/* --- Contributor information ---
 * Please follow the following set of guidelines when working on this plugin,
 * this to help others understand this file more easily.
 *
 * -- Authors --
 * Thimo (ThibmoRozier) <thibmorozier@live.nl>
 * rfc1920 <no@email.com>
 * Mheetu <no@email.com>
 *
 * -- Naming --
 * Avoid using non-alphabetic characters, eg: _
 * Avoid using numbers in method and class names (Upgrade methods are allowed to have these, for readability)
 * Private constants -------------------- SHOULD start with a uppercase "C" (PascalCase)
 * Private readonly fields -------------- SHOULD start with a uppercase "C" (PascalCase)
 * Private fields ----------------------- SHOULD start with a uppercase "F" (PascalCase)
 * Arguments/Parameters ----------------- SHOULD start with a lowercase "a" (camelCase)
 * Classes ------------------------------ SHOULD start with a uppercase character (PascalCase)
 * Methods ------------------------------ SHOULD start with a uppercase character (PascalCase)
 * Public properties (constants/fields) - SHOULD start with a uppercase character (PascalCase)
 * Variables ---------------------------- SHOULD start with a lowercase character (camelCase)
 *
 * -- Style --
 * Single-line comments - // Single-line comment
 * Multi-line comments -- Just like this comment block!
 */
using Newtonsoft.Json;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("PlayerAdministration", "ThibmoRozier", "1.3.21")]
    [Description("Allows server admins to moderate users using a GUI from within the game.")]
    public class PlayerAdministration : RustPlugin
    {
        #region Plugin References
#pragma warning disable IDE0044, CS0649
        [PluginReference]
        private Plugin Economics;
        [PluginReference]
        private Plugin ServerRewards;
        [PluginReference]
        private Plugin Freeze;
        [PluginReference]
        private Plugin PermissionsManager;
        [PluginReference]
        private Plugin DiscordMessages;
        [PluginReference]
        private Plugin BetterChatMute;
#pragma warning restore IDE0044, CS0649
        #endregion Plugin References

        #region GUI
        #region Types
        /// <summary>
        /// UI Color object
        /// </summary>
        private class CuiColor
        {
            public byte R { get; set; } = 255;
            public byte G { get; set; } = 255;
            public byte B { get; set; } = 255;
            public float A { get; set; } = 1f;

            public CuiColor() { }

            public CuiColor(byte aRed, byte aGreen, byte aBlue, float aAlpha = 1f)
            {
                R = aRed;
                G = aGreen;
                B = aBlue;
                A = aAlpha;
            }

            public override string ToString() =>
                $"{(double)R / 255} {(double)G / 255} {(double)B / 255} {A}";
        }

        /// <summary>
        /// Element position object
        /// </summary>
        private class CuiPoint
        {
            public float X { get; set; } = 0f;
            public float Y { get; set; } = 0f;

            public CuiPoint() { }

            public CuiPoint(float aX, float aY)
            {
                X = aX;
                Y = aY;
            }

            public override string ToString() =>
                $"{X} {Y}";
        }

        /// <summary>
        /// UI pages to make the switching more humanly readable
        /// </summary>
        private enum UiPage
        {
            Main = 0,
            PlayersOnline,
            PlayersOffline,
            PlayersBanned,
            PlayerPage,
            PlayerPageBanned
        }
        #endregion Types

        #region Defaults
        /// <summary>
        /// Predefined default color set
        /// </summary>
        private static class CuiDefaultColors
        {
            public static readonly CuiColor Background = new CuiColor(240, 240, 240, 0.3f);
            public static readonly CuiColor BackgroundMedium = new CuiColor(76, 74, 72, 0.83f);
            public static readonly CuiColor BackgroundDark = new CuiColor(42, 42, 42, 0.93f);
            public static readonly CuiColor Button = new CuiColor(42, 42, 42, 1f);
            public static readonly CuiColor ButtonInactive = new CuiColor(168, 168, 168, 1f);
            public static readonly CuiColor ButtonDecline = new CuiColor(192, 0, 0, 1f);
            public static readonly CuiColor ButtonDanger = new CuiColor(193, 46, 42, 1f);
            public static readonly CuiColor ButtonWarning = new CuiColor(213, 133, 18, 1f);
            public static readonly CuiColor ButtonSuccess = new CuiColor(57, 132, 57, 1f);
            public static readonly CuiColor Text = new CuiColor(0, 0, 0, 1f);
            public static readonly CuiColor TextAlt = new CuiColor(255, 255, 255, 1f);
            public static readonly CuiColor TextTitle = new CuiColor(206, 66, 43, 1f);
            public static readonly CuiColor None = new CuiColor(0, 0, 0, 0f);
        }
        #endregion Defaults

        #region UI object definitions
        /// <summary>
        /// Input field object
        /// </summary>
        private class CuiInputField
        {
            public CuiInputFieldComponent InputField { get; } = new CuiInputFieldComponent();
            public CuiRectTransformComponent RectTransform { get; } = new CuiRectTransformComponent();
            public float FadeOut { get; set; }
        }
        #endregion UI object definitions

        #region Component container
        /// <summary>
        /// Custom version of the CuiElementContainer to add InputFields
        /// </summary>
        private class CustomCuiElementContainer : CuiElementContainer
        {
            public string Add(CuiInputField aInputField, string aParent = Cui.ParentHud, string aName = "")
            {
                if (string.IsNullOrEmpty(aName))
                    aName = CuiHelper.GetGuid();

                if (aInputField == null) {
                    FPluginInstance.LogError($"CustomCuiElementContainer::Add > Parameter 'aInputField' is null");
                    return string.Empty;
                }

                Add(new CuiElement {
                    Name = aName,
                    Parent = aParent,
                    FadeOut = aInputField.FadeOut,
                    Components = {
                        aInputField.InputField,
                        aInputField.RectTransform
                    }
                });
                return aName;
            }
        }
        #endregion Component container

        /// <summary>
        /// Rust UI object
        /// </summary>
        private class Cui
        {
            public const string ParentHud = "Hud";
            public const string ParentOverlay = "Overlay";
            public string MainPanelName { get; set; }
            private readonly BasePlayer FPlayer;
            private readonly CustomCuiElementContainer FContainer = new CustomCuiElementContainer();

            /// <summary>
            /// Constructor
            /// </summary>
            /// <param name="aPlayer">The player this object is meant for</param>
            public Cui(BasePlayer aPlayer)
            {
                if (aPlayer == null) {
                    FPluginInstance.LogError($"Cui::Cui > Parameter 'aPlayer' is null");
                    return;
                }

                FPlayer = aPlayer;
                FPluginInstance.LogDebug("Cui instance created");
            }

            /// <summary>
            /// Add a new panel
            /// </summary>
            /// <param name="aParent">The parent object name</param>
            /// <param name="aLeftBottomAnchor">Left(x)-Bottom(y) relative position</param>
            /// <param name="aRightTopAnchor">Right(x)-Top(y) relative position</param>
            /// <param name="aIndCursorEnabled">The panel requires the cursor</param>
            /// <param name="aColor">Image color</param>
            /// <param name="aName">The object's name</param>
            /// <param name="aPng">Image PNG file path</param>
            /// <returns>New object name</returns>
            public string AddPanel(string aParent, CuiPoint aLeftBottomAnchor, CuiPoint aRightTopAnchor, bool aIndCursorEnabled, CuiColor aColor = null,
                                   string aName = "", string aPng = "") =>
                AddPanel(aParent, aLeftBottomAnchor, aRightTopAnchor, new CuiPoint(), new CuiPoint(), aIndCursorEnabled, aColor, aName, aPng);

            /// <summary>
            /// Add a new panel
            /// </summary>
            /// <param name="aParent">The parent object name</param>
            /// <param name="aLeftBottomAnchor">Left(x)-Bottom(y) relative position</param>
            /// <param name="aRightTopAnchor">Right(x)-Top(y) relative position</param>
            /// <param name="aLeftBottomOffset">Left(x)-Bottom(y) relative offset</param>
            /// <param name="aRightTopOffset">Right(x)-Top(y) relative offset</param>
            /// <param name="aIndCursorEnabled">The panel requires the cursor</param>
            /// <param name="aColor">Image color</param>
            /// <param name="aName">The object's name</param>
            /// <param name="aPng">Image PNG file path</param>
            /// <returns>New object name</returns>
            public string AddPanel(string aParent, CuiPoint aLeftBottomAnchor, CuiPoint aRightTopAnchor, CuiPoint aLeftBottomOffset, CuiPoint aRightTopOffset,
                                   bool aIndCursorEnabled, CuiColor aColor = null, string aName = "", string aPng = "")
            {
                if (aLeftBottomAnchor == null || aRightTopAnchor == null || aLeftBottomOffset == null || aRightTopOffset == null) {
                    FPluginInstance.LogError($"Cui::AddPanel > One of the required parameters is null");
                    return string.Empty;
                }

                CuiPanel panel = new CuiPanel() {
                    RectTransform = {
                        AnchorMin = aLeftBottomAnchor.ToString(),
                        AnchorMax = aRightTopAnchor.ToString(),
                        OffsetMin = aLeftBottomOffset.ToString(),
                        OffsetMax = aRightTopOffset.ToString()
                    },
                    CursorEnabled = aIndCursorEnabled
                };

                if (!string.IsNullOrEmpty(aPng))
                    panel.Image = new CuiImageComponent() {
                        Png = aPng
                    };

                if (aColor != null) {
                    if (panel.Image == null) {
                        panel.Image = new CuiImageComponent() {
                            Color = aColor.ToString()
                        };
                    } else {
                        panel.Image.Color = aColor.ToString();
                    }
                }

                FPluginInstance.LogDebug("Added panel to container");
                return FContainer.Add(panel, aParent, string.IsNullOrEmpty(aName) ? null : aName);
            }

            /// <summary>
            /// Add a new label
            /// </summary>
            /// <param name="aParent">The parent object name</param>
            /// <param name="aLeftBottomAnchor">Left(x)-Bottom(y) relative position</param>
            /// <param name="aRightTopAnchor">Right(x)-Top(y) relative position</param>
            /// <param name="aColor">Text color</param>
            /// <param name="aText">Text to show</param>
            /// <param name="aName">The object's name</param>
            /// <param name="aFontSize">Font size</param>
            /// <param name="aAlign">Text alignment</param>
            /// <returns>New object name</returns>
            public string AddLabel(string aParent, CuiPoint aLeftBottomAnchor, CuiPoint aRightTopAnchor, CuiColor aColor, string aText, string aName = "",
                                   int aFontSize = 14, TextAnchor aAlign = TextAnchor.UpperLeft) =>
                AddLabel(aParent, aLeftBottomAnchor, aRightTopAnchor, new CuiPoint(), new CuiPoint(), aColor, aText, aName, aFontSize, aAlign);

            /// <summary>
            /// Add a new label
            /// </summary>
            /// <param name="aParent">The parent object name</param>
            /// <param name="aLeftBottomAnchor">Left(x)-Bottom(y) relative position</param>
            /// <param name="aRightTopAnchor">Right(x)-Top(y) relative position</param>
            /// <param name="aLeftBottomOffset">Left(x)-Bottom(y) relative offset</param>
            /// <param name="aRightTopOffset">Right(x)-Top(y) relative offset</param>
            /// <param name="aColor">Text color</param>
            /// <param name="aText">Text to show</param>
            /// <param name="aName">The object's name</param>
            /// <param name="aFontSize">Font size</param>
            /// <param name="aAlign">Text alignment</param>
            /// <returns>New object name</returns>
            public string AddLabel(string aParent, CuiPoint aLeftBottomAnchor, CuiPoint aRightTopAnchor, CuiPoint aLeftBottomOffset, CuiPoint aRightTopOffset,
                                   CuiColor aColor, string aText, string aName = "", int aFontSize = 14, TextAnchor aAlign = TextAnchor.UpperLeft)
            {
                if (aLeftBottomAnchor == null || aRightTopAnchor == null || aLeftBottomOffset == null || aRightTopOffset == null || aColor == null) {
                    FPluginInstance.LogError($"Cui::AddLabel > One of the required parameters is null");
                    return string.Empty;
                }

                FPluginInstance.LogDebug("Added label to container");
                return FContainer.Add(new CuiLabel() {
                    Text = {
                        Text = aText ?? string.Empty,
                        FontSize = aFontSize,
                        Align = aAlign,
                        Color = aColor.ToString()
                    },
                    RectTransform = {
                        AnchorMin = aLeftBottomAnchor.ToString(),
                        AnchorMax = aRightTopAnchor.ToString(),
                        OffsetMin = aLeftBottomOffset.ToString(),
                        OffsetMax = aRightTopOffset.ToString()
                    }
                }, aParent, string.IsNullOrEmpty(aName) ? null : aName);
            }

            /// <summary>
            /// Add a new button
            /// </summary>
            /// <param name="aParent">The parent object name</param>
            /// <param name="aLeftBottomAnchor">Left(x)-Bottom(y) relative position</param>
            /// <param name="aRightTopAnchor">Right(x)-Top(y) relative position</param>
            /// <param name="aButtonColor">Button background color</param>
            /// <param name="aTextColor">Text color</param>
            /// <param name="aText">Text to show</param>
            /// <param name="aCommand">OnClick event callback command</param>
            /// <param name="aClose">Panel to close</param>
            /// <param name="aName">The object's name</param>
            /// <param name="aFontSize">Font size</param>
            /// <param name="aAlign">Text alignment</param>
            /// <returns>New object name</returns>
            public string AddButton(string aParent, CuiPoint aLeftBottomAnchor, CuiPoint aRightTopAnchor, CuiColor aButtonColor, CuiColor aTextColor, string aText,
                                    string aCommand = "", string aClose = "", string aName = "", int aFontSize = 14, TextAnchor aAlign = TextAnchor.MiddleCenter) =>
                AddButton(aParent, aLeftBottomAnchor, aRightTopAnchor, new CuiPoint(), new CuiPoint(), aButtonColor, aTextColor, aText, aCommand, aClose, aName,
                          aFontSize, aAlign);

            /// <summary>
            /// Add a new button
            /// </summary>
            /// <param name="aParent">The parent object name</param>
            /// <param name="aLeftBottomAnchor">Left(x)-Bottom(y) relative position</param>
            /// <param name="aRightTopAnchor">Right(x)-Top(y) relative position</param>
            /// <param name="aLeftBottomOffset">Left(x)-Bottom(y) relative offset</param>
            /// <param name="aRightTopOffset">Right(x)-Top(y) relative offset</param>
            /// <param name="aButtonColor">Button background color</param>
            /// <param name="aTextColor">Text color</param>
            /// <param name="aText">Text to show</param>
            /// <param name="aCommand">OnClick event callback command</param>
            /// <param name="aClose">Panel to close</param>
            /// <param name="aName">The object's name</param>
            /// <param name="aFontSize">Font size</param>
            /// <param name="aAlign">Text alignment</param>
            /// <returns>New object name</returns>
            public string AddButton(string aParent, CuiPoint aLeftBottomAnchor, CuiPoint aRightTopAnchor, CuiPoint aLeftBottomOffset, CuiPoint aRightTopOffset,
                                    CuiColor aButtonColor, CuiColor aTextColor, string aText, string aCommand = "", string aClose = "", string aName = "",
                                    int aFontSize = 14, TextAnchor aAlign = TextAnchor.MiddleCenter)
            {
                if (aLeftBottomAnchor == null || aRightTopAnchor == null || aLeftBottomOffset == null || aRightTopOffset == null ||
                    aButtonColor == null || aTextColor == null) {
                    FPluginInstance.LogError($"Cui::AddButton > One of the required parameters is null");
                    return string.Empty;
                }

                FPluginInstance.LogDebug("Added button to container");
                return FContainer.Add(new CuiButton() {
                    Button = {
                        Command = aCommand ?? string.Empty,
                        Close = aClose ?? string.Empty,
                        Color = aButtonColor.ToString()
                    },
                    RectTransform = {
                        AnchorMin = aLeftBottomAnchor.ToString(),
                        AnchorMax = aRightTopAnchor.ToString(),
                        OffsetMin = aLeftBottomOffset.ToString(),
                        OffsetMax = aRightTopOffset.ToString()
                    },
                    Text = {
                        Text = aText ?? string.Empty,
                        FontSize = aFontSize,
                        Align = aAlign,
                        Color = aTextColor.ToString()
                    }
                }, aParent, string.IsNullOrEmpty(aName) ? null : aName);
            }

            /// <summary>
            /// Add a new input field
            /// </summary>
            /// <param name="aParent">The parent object name</param>
            /// <param name="aLeftBottomAnchor">Left(x)-Bottom(y) relative position</param>
            /// <param name="aRightTopAnchor">Right(x)-Top(y) relative position</param>
            /// <param name="aColor">Text color</param>
            /// <param name="aText">Text to show</param>
            /// <param name="aCharsLimit">Max character count</param>
            /// <param name="aCommand">OnChanged event callback command</param>
            /// <param name="aIndPassword">Indicates that this input should show password chars</param>
            /// <param name="aName">The object's name</param>
            /// <param name="aFontSize">Font size</param>
            /// <param name="aAlign">Text alignment</param>
            /// <returns>New object name</returns>
            public string AddInputField(string aParent, CuiPoint aLeftBottomAnchor, CuiPoint aRightTopAnchor, CuiColor aColor, string aText = "",
                                        int aCharsLimit = 100, string aCommand = "", bool aIndPassword = false, string aName = "", int aFontSize = 14,
                                        TextAnchor aAlign = TextAnchor.MiddleLeft) =>
                AddInputField(aParent, aLeftBottomAnchor, aRightTopAnchor, new CuiPoint(), new CuiPoint(), aColor, aText, aCharsLimit, aCommand, aIndPassword, aName,
                              aFontSize, aAlign);

            /// <summary>
            /// Add a new input field
            /// </summary>
            /// <param name="aParent">The parent object name</param>
            /// <param name="aLeftBottomAnchor">Left(x)-Bottom(y) relative position</param>
            /// <param name="aRightTopAnchor">Right(x)-Top(y) relative position</param>
            /// <param name="aLeftBottomOffset">Left(x)-Bottom(y) relative offset</param>
            /// <param name="aRightTopOffset">Right(x)-Top(y) relative offset</param>
            /// <param name="fadeOut">Fade-out time</param>
            /// <param name="aColor">Text color</param>
            /// <param name="aText">Text to show</param>
            /// <param name="aCharsLimit">Max character count</param>
            /// <param name="aCommand">OnChanged event callback command</param>
            /// <param name="aIndPassword">Indicates that this input should show password chars</param>
            /// <param name="aName">The object's name</param>
            /// <param name="aFontSize">Font size</param>
            /// <returns>New object name</returns>
            public string AddInputField(string aParent, CuiPoint aLeftBottomAnchor, CuiPoint aRightTopAnchor, CuiPoint aLeftBottomOffset, CuiPoint aRightTopOffset,
                                        CuiColor aColor, string aText = "", int aCharsLimit = 100, string aCommand = "", bool aIndPassword = false,
                                        string aName = "", int aFontSize = 14, TextAnchor aAlign = TextAnchor.MiddleLeft)
            {
                if (aLeftBottomAnchor == null || aRightTopAnchor == null || aLeftBottomOffset == null || aRightTopOffset == null || aColor == null) {
                    FPluginInstance.LogError($"Cui::AddInputField > One of the required parameters is null");
                    return string.Empty;
                }

                FPluginInstance.LogDebug("Added input field to container");
                return FContainer.Add(new CuiInputField() {
                    InputField = {
                        Text = aText ?? string.Empty,
                        FontSize = aFontSize,
                        Align = aAlign,
                        Color = aColor.ToString(),
                        CharsLimit = aCharsLimit,
                        Command = aCommand ?? string.Empty,
                        IsPassword = aIndPassword
                    },
                    RectTransform = {
                        AnchorMin = aLeftBottomAnchor.ToString(),
                        AnchorMax = aRightTopAnchor.ToString(),
                        OffsetMin = aLeftBottomOffset.ToString(),
                        OffsetMax = aRightTopOffset.ToString()
                    }
                }, aParent, string.IsNullOrEmpty(aName) ? null : aName);
            }

            /// <summary>
            /// Draw the UI to the player's client
            /// </summary>
            /// <returns></returns>
            public bool Draw()
            {
                if (!string.IsNullOrEmpty(MainPanelName)) {
                    FPluginInstance.LogDebug("Sent the container for drawing to the client");
                    return CuiHelper.AddUi(FPlayer, FContainer);
                }

                return false;
            }

            /// <summary>
            /// Retrieve the userId of the player this GUI is intended for
            /// </summary>
            /// <returns>Player ID</returns>
            public string GetPlayerId() =>
                FPlayer.UserIDString;
        }
        #endregion GUI

        #region Utility methods
        /// <summary>
        /// Get a "page" of entities from a specified list
        /// </summary>
        /// <param name="aList">List of entities</param>
        /// <param name="aPage">Page number (Starting from 0)</param>
        /// <param name="aPageSize">Page size</param>
        /// <returns>List of entities</returns>
        private List<T> GetPage<T>(IList<T> aList, int aPage, int aPageSize) =>
            aList.Skip(aPage * aPageSize).Take(aPageSize).ToList();

        /// <summary>
        /// Add a button to the tab menu
        /// </summary>
        /// <param name="aUIObj">Cui object</param>
        /// <param name="aParent">Name of the parent object</param>
        /// <param name="aCaption">Text to show</param>
        /// <param name="aCommand">Button to execute</param>
        /// <param name="aPos">Bounds of the button</param>
        /// <param name="aIndActive">To indicate whether or not the button is active</param>
        private void AddTabMenuBtn(ref Cui aUIObj, string aParent, string aCaption, string aCommand, int aPos, bool aIndActive)
        {
            Vector2 dimensions = new Vector2(0.096f, 0.75f);
            Vector2 offset = new Vector2(0.005f, 0.1f);
            CuiColor btnColor = (aIndActive ? CuiDefaultColors.ButtonInactive : CuiDefaultColors.Button);
            CuiPoint lbAnchor = new CuiPoint(((dimensions.x + offset.x) * aPos) + offset.x, offset.y);
            CuiPoint rtAnchor = new CuiPoint(lbAnchor.X + dimensions.x, offset.y + dimensions.y);
            aUIObj.AddButton(aParent, lbAnchor, rtAnchor, btnColor, CuiDefaultColors.TextAlt, aCaption, (aIndActive ? string.Empty : aCommand));
        }

        /// <summary>
        /// Add a set of user buttons to the parent object
        /// </summary>
        /// <param name="aUIObj">Cui object</param>
        /// <param name="aParent">Name of the parent object</param>
        /// <param name="aUserList">List of entities</param>
        /// <param name="aCommandFmt">Base format of the command to execute (Will be completed with the user ID</param>
        /// <param name="aPage">User list page</param>
        private void AddPlayerButtons<T>(ref Cui aUIObj, string aParent, ref List<T> aUserList, string aCommandFmt, int aPage)
        {
            List<T> userRange = GetPage(aUserList, aPage, CMaxPlayerButtons);
            Vector2 dimensions = new Vector2(0.194f, 0.06f);
            Vector2 offset = new Vector2(0.005f, 0.01f);
            int col = -1;
            int row = 0;
            float margin = 0.09f;
            List<string> addedNames = new List<string>();

            foreach (T user in userRange) {
                if (++col >= CMaxPlayerCols) {
                    row++;
                    col = 0;
                }

                float calcTop = (1f - margin) - (((dimensions.y + offset.y) * row) + offset.y);
                float calcLeft = ((dimensions.x + offset.x) * col) + offset.x;
                CuiPoint lbAnchor = new CuiPoint(calcLeft, calcTop - dimensions.y);
                CuiPoint rtAnchor = new CuiPoint(calcLeft + dimensions.x, calcTop);
                string btnText;
                string btnCommand;
                int suffix = 0;

                if (typeof(T) == typeof(BasePlayer)) {
                    BasePlayer player = (user as BasePlayer);
                    btnText = player.displayName;
                    btnCommand = string.Format(aCommandFmt, player.UserIDString);

                    while (addedNames.FindIndex(item => string.Equals(btnText, item, StringComparison.OrdinalIgnoreCase)) >= 0)
                        btnText = $"{player.displayName} {++suffix}";

                    aUIObj.AddButton(aParent, lbAnchor, rtAnchor, CuiDefaultColors.Button, CuiDefaultColors.TextAlt, btnText, btnCommand, string.Empty, string.Empty, 16);
                } else {
                    ServerUsers.User player = (user as ServerUsers.User);
                    string btnTextTemp = player.username;
                    btnCommand = string.Format(aCommandFmt, player.steamid);

                    if (string.IsNullOrEmpty(btnTextTemp) || CUnknownNameList.Contains(btnTextTemp.ToLower()))
                        btnTextTemp = player.steamid.ToString();

                    btnText = btnTextTemp;

                    while (addedNames.FindIndex(item => string.Equals(btnText, item, StringComparison.OrdinalIgnoreCase)) >= 0)
                        btnText = $"{btnTextTemp} {++suffix}";

                    aUIObj.AddButton(aParent, lbAnchor, rtAnchor, CuiDefaultColors.Button, CuiDefaultColors.TextAlt, btnText, btnCommand, string.Empty, string.Empty, 16);
                }

                addedNames.Add(btnText);
            }

            LogDebug("Added the player buttons to the container");
        }

        /// <summary>
        /// Get translated message for the specified key
        /// </summary>
        /// <param name="aKey">Message key</param>
        /// <param name="aPlayerId">Player ID</param>
        /// <param name="aArgs">Optional args</param>
        /// <returns></returns>
        private string GetMessage(string aKey, string aPlayerId, params object[] aArgs) =>
            string.Format(lang.GetMessage(aKey, this, aPlayerId), aArgs);

        /// <summary>
        /// Log an error message to the logfile
        /// </summary>
        /// <param name="aMessage"></param>
        private void LogError(string aMessage) =>
            LogToFile(string.Empty, $"[{DateTime.Now.ToString("hh:mm:ss")}] ERROR > {aMessage}", this);

        /// <summary>
        /// Log an informational message to the logfile
        /// </summary>
        /// <param name="aMessage"></param>
        private void LogInfo(string aMessage) =>
            LogToFile(string.Empty, $"[{DateTime.Now.ToString("hh:mm:ss")}] INFO > {aMessage}", this);

        /// <summary>
        /// Log a debugging message to the logfile
        /// </summary>
        /// <param name="aMessage"></param>
        private void LogDebug(string aMessage)
        {
            if (CDebugEnabled)
                LogToFile(string.Empty, $"[{DateTime.Now.ToString("hh:mm:ss")}] DEBUG > {aMessage}", this);
        }

        /// <summary>
        /// Send a message to a specific player
        /// </summary>
        /// <param name="aPlayer">The player to send the message to</param>
        /// <param name="aMessage">The message to send</param>
        private void SendMessage(ref BasePlayer aPlayer, string aMessage) =>
            rust.SendChatMessage(aPlayer, string.Empty, aMessage);

        /// <summary>
        /// Verify if a user has the specified permission
        /// </summary>
        /// <param name="aPlayer">The player</param>
        /// <param name="aPermission">Pass <see cref="string.Empty"/> to only verify <see cref="CPermUiShow"/></param>
        /// <param name="aIndReport">Indicates that issues should be reported</param>
        /// <returns></returns>
        private bool VerifyPermission(ref BasePlayer aPlayer, string aPermission, bool aIndReport = false)
        {
            bool result = permission.UserHasPermission(aPlayer.UserIDString, CPermUiShow);
            aPermission = aPermission ?? string.Empty; // We need to get rid of possible null values
            
            if (FConfigData.UsePermSystem && result && aPermission.Length > 0)
                result = permission.UserHasPermission(aPlayer.UserIDString, aPermission);

            if (aIndReport && !result) {
                SendMessage(ref aPlayer, GetMessage("Permission Error Text", aPlayer.UserIDString));
                LogError(GetMessage("Permission Error Log Text", aPlayer.UserIDString, aPlayer.displayName, aPermission));
            }

            return result;
        }

        /// <summary>
        /// Verify if a user has the specified permission
        /// </summary>
        /// <param name="aPlayerId">The player's ID</param>
        /// <param name="aPermission">Pass <see cref="string.Empty"/> to only verify <see cref="CPermUiShow"/></param>
        /// <returns></returns>
        private bool VerifyPermission(string aPlayerId, string aPermission)
        {
            BasePlayer player = BasePlayer.Find(aPlayerId);
            return VerifyPermission(ref player, aPermission);
        }

        /// <summary>
        /// Retrieve server users
        /// </summary>
        /// <param name="aIndFiltered">Indicates if the output should be filtered</param>
        /// <param name="aUserId">User ID for retrieving filter text</param>
        /// <param name="aIndOffline">Retrieve the list of sleepers (offline players)</param>
        /// <returns></returns>
        private List<BasePlayer> GetServerUserList(bool aIndFiltered, string aUserId, bool aIndOffline = false)
        {
            List<BasePlayer> result = new List<BasePlayer>();
            ulong userId = ulong.Parse(aUserId);

            if (aIndOffline) {
                Player.Sleepers.ForEach(user => {
                    ServerUsers.User servUser = ServerUsers.Get(user.userID);

                    if (servUser == null || servUser?.group != ServerUsers.UserGroup.Banned)
                        result.Add(user);
                });
            } else {
                Player.Players.ForEach(user => {
                    ServerUsers.User servUser = ServerUsers.Get(user.userID);

                    if (servUser == null || servUser?.group != ServerUsers.UserGroup.Banned)
                        result.Add(user);
                });
            }

            if (aIndFiltered && FUserBtnPageSearchInputText.ContainsKey(userId))
                result = result.Where(x => x.displayName.IndexOf(FUserBtnPageSearchInputText[userId], StringComparison.OrdinalIgnoreCase) >= 0 ||
                                           x.UserIDString.IndexOf(FUserBtnPageSearchInputText[userId], StringComparison.OrdinalIgnoreCase) >= 0).ToList();

            LogDebug("Retrieved the server user list");
            result.Sort((a, b) => {
                int diff = string.Compare(a.displayName, b.displayName);

                if (diff == 0)
                    diff = a.userID.CompareTo(b.userID);

                return diff;
            });
            return result;
        }

        /// <summary>
        /// Retrieve server users
        /// </summary>
        /// <param name="aIndFiltered">Indicates if the output should be filtered</param>
        /// <param name="aUserId">User ID for retrieving filter text</param>
        /// <returns></returns>
        private List<ServerUsers.User> GetBannedUserList(bool aIndFiltered, string aUserId)
        {
            List<ServerUsers.User> result = ServerUsers.GetAll(ServerUsers.UserGroup.Banned).ToList();
            ulong userId = ulong.Parse(aUserId);

            if (aIndFiltered && FUserBtnPageSearchInputText.ContainsKey(userId))
                result = result.Where(x => x.username.IndexOf(FUserBtnPageSearchInputText[userId], StringComparison.OrdinalIgnoreCase) >= 0 ||
                                           x.steamid.ToString().IndexOf(FUserBtnPageSearchInputText[userId], StringComparison.OrdinalIgnoreCase) >= 0).ToList();

            LogDebug("Retrieved the banned user list");
            result.Sort((a, b) => {
                int diff = string.Compare(a.username, b.username);

                if (diff == 0)
                    diff = a.steamid.CompareTo(b.steamid);

                return diff;
            });
            return result;
        }

        /// <summary>
        /// Retrieve the target player ID from the arguments and report success
        /// </summary>
        /// <param name="aArg">Argument object</param>
        /// <param name="aTarget">Player ID</param>
        /// <returns></returns>
        private bool GetTargetFromArg(ref ConsoleSystem.Arg aArg, out ulong aTarget)
        {
            aTarget = 0;

            if (!aArg.HasArgs() || !ulong.TryParse(aArg.Args[0], out aTarget))
                return false;

            return true;
        }

        /// <summary>
        /// Retrieve the target player ID and amount from the arguments and report success
        /// </summary>
        /// <param name="aArg">Argument object</param>
        /// <param name="aTarget">Player ID</param>
        /// <param name="aAmount">Amount</param>
        /// <returns></returns>
        private bool GetTargetAmountFromArg(ref ConsoleSystem.Arg aArg, out ulong aTarget, out float aAmount)
        {
            aTarget = 0;
            aAmount = 0;

            if (!aArg.HasArgs(2) || !ulong.TryParse(aArg.Args[0], out aTarget) || !float.TryParse(aArg.Args[1], out aAmount))
                return false;

            return true;
        }

        /// <summary>
        /// Check if the player has the VoiceMuted flag set
        /// </summary>
        /// <param name="aPlayer">The player</param>
        /// <returns></returns>
        private bool GetIsVoiceMuted(ref BasePlayer aPlayer) =>
            aPlayer.HasPlayerFlag(BasePlayer.PlayerFlags.VoiceMuted);

        /// <summary>
        /// Check if the player has the ChatMute flag set
        /// </summary>
        /// <param name="aPlayer">The player</param>
        /// <returns></returns>
        private bool GetIsChatMuted(ref BasePlayer aPlayer)
        {
            bool isServerMuted = aPlayer.HasPlayerFlag(BasePlayer.PlayerFlags.ChatMute);

            if (BetterChatMute != null) {
                return isServerMuted || (bool)BetterChatMute.Call("API_IsMuted", aPlayer.IPlayer);
            } else {
                return isServerMuted;
            }
        }

        /// <summary>
        /// Check if the player has the freeze.frozen permission
        /// </summary>
        /// <param name="aPlayerId">The player's ID</param>
        /// <returns></returns>
        private bool GetIsFrozen(ulong aPlayerId) =>
            permission.UserHasPermission(aPlayerId.ToString(), CPermFreezeFrozen);

        /// <summary>
        /// Send either a kick or a ban message to Discord via the DiscordMessages plugin
        /// </summary>
        /// <param name="aAdminName">The name of the admin</param>
        /// <param name="aAdminId">The ID of the admin</param>
        /// <param name="aTargetName">The name of the target player</param>
        /// <param name="aTargetId">The ID of the target player</param>
        /// <param name="aReason">The reason message</param>
        /// <param name="aIndIsBan">If this is true a ban message is sent, else a kick message is sent</param>
        private void SendDiscordKickBanMessage(string aAdminName, string aAdminId, string aTargetName, string aTargetId, string aReason, bool aIndIsBan)
        {
            if (DiscordMessages != null) {
                if (CUnknownNameList.Contains(aTargetName.ToLower()))
                    aTargetName = aTargetId;

                object fields = new[] {
                    new {
                        name = "Player",
                        value = $"[{aTargetName}](https://steamcommunity.com/profiles/{aTargetId})",
                        inline = true
                    },
                    new {
                        name = aIndIsBan ? "Banned by" : "Kicked by",
                        value = $"[{aAdminName}](https://steamcommunity.com/profiles/{aAdminId})",
                        inline = true
                    },
                    new {
                        name = "Reason",
                        value = aReason,
                        inline = false
                    }
                };
                DiscordMessages.Call(
                    "API_SendFancyMessage",
                    aIndIsBan ? FConfigData.BanMsgWebhookUrl : FConfigData.KickMsgWebhookUrl,
                    aIndIsBan ? "Player Ban" : "Player Kick",
                    3329330,
                    JsonConvert.SerializeObject(fields)
                );
            }
        }
        #endregion Utility methods

        #region Upgrade methods
        /// <summary>
        /// Upgrade the config to 1.3.10 if needed
        /// </summary>
        /// <returns></returns>
        private bool UpgradeTo1310()
        {
            bool result = false;
            Config.Load();

            if (Config["Use Permission System"] == null) {
                FConfigData.UsePermSystem = true;
                result = true;
            }

            // Remove legacy config items
            if (Config["Enable kick action"] != null || Config["Enable ban action"] != null || Config["Enable unban action"] != null ||
                Config["Enable kill action"] != null || Config["Enable inventory clear action"] != null || Config["Enable blueprint reset action"] != null ||
                Config["Enable metabolism reset action"] != null || Config["Enable hurt action"] != null || Config["Enable heal action"] != null ||
                Config["Enable voice mute action"] != null || Config["Enable chat mute action"] != null || Config["Enable perms action"] != null ||
                Config["Enable freeze action"] != null)
                result = true;

            Config.Clear();

            if (result)
                Config.WriteObject(FConfigData);

            return result;
        }

        /// <summary>
        /// Upgrade the config to 1.3.13 if needed
        /// </summary>
        /// <returns></returns>
        private bool UpgradeTo1313()
        {
            bool result = false;
            Config.Load();

            if (Config["Discord Webhook url for ban messages"] == null) {
                FConfigData.BanMsgWebhookUrl = string.Empty;
                result = true;
            }

            if (Config["Discord Webhook url for kick messages"] == null) {
                FConfigData.KickMsgWebhookUrl = string.Empty;
                result = true;
            }

            Config.Clear();

            if (result)
                Config.WriteObject(FConfigData);

            return result;
        }
        #endregion

        #region GUI build methods
        /// <summary>
        /// Build the tab nav-bar
        /// </summary>
        /// <param name="aUIObj">Cui object</param>
        /// <param name="aPageType">The active page type</param>
        private void BuildTabMenu(ref Cui aUIObj, UiPage aPageType)
        {
            string uiUserId = aUIObj.GetPlayerId();
            // Add the panels and title label
            string headerPanel = aUIObj.AddPanel(aUIObj.MainPanelName, CMainMenuHeaderContainerLbAnchor, CMainMenuHeaderContainerRtAnchor, false,
                                                 CuiDefaultColors.None);
            string tabBtnPanel = aUIObj.AddPanel(aUIObj.MainPanelName, CMainMenuTabBtnContainerLbAnchor, CMainMenuTabBtnContainerRtAnchor, false,
                                                 CuiDefaultColors.Background);
            aUIObj.AddLabel(headerPanel, CMainMenuHeaderLblLbAnchor, CMainMenuHeaderLblRtAnchor, CuiDefaultColors.TextTitle,
                            "Player Administration by ThibmoRozier", string.Empty, 22, TextAnchor.MiddleCenter);
            aUIObj.AddButton(headerPanel, CMainMenuCloseBtnLbAnchor, CMainMenuCloseBtnRtAnchor, CuiDefaultColors.ButtonDecline, CuiDefaultColors.TextAlt, "X",
                             CCloseUiCmd, string.Empty, string.Empty, 22);
            // Add the tab menu buttons
            AddTabMenuBtn(ref aUIObj, tabBtnPanel, GetMessage("Main Tab Text", uiUserId), $"{CSwitchUiCmd} {CCmdArgMain}", 0, (aPageType == UiPage.Main ? true : false));
            AddTabMenuBtn(ref aUIObj, tabBtnPanel, GetMessage("Online Player Tab Text", uiUserId), $"{CSwitchUiCmd} {CCmdArgPlayersOnline} 0", 1,
                          (aPageType == UiPage.PlayersOnline ? true : false));
            AddTabMenuBtn(ref aUIObj, tabBtnPanel, GetMessage("Offline Player Tab Text", uiUserId), $"{CSwitchUiCmd} {CCmdArgPlayersOffline} 0", 2,
                          (aPageType == UiPage.PlayersOffline ? true : false));
            AddTabMenuBtn(ref aUIObj, tabBtnPanel, GetMessage("Banned Player Tab Text", uiUserId), $"{CSwitchUiCmd} {CCmdArgPlayersBanned} 0", 3,
                          (aPageType == UiPage.PlayersBanned ? true : false));
            LogDebug("Built the tab menu");
        }

        /// <summary>
        /// Build the main-menu
        /// </summary>
        /// <param name="aUIObj">Cui object</param>
        private void BuildMainPage(ref Cui aUIObj)
        {
            string uiUserId = aUIObj.GetPlayerId();
            // Add the panels and title
            string panel = aUIObj.AddPanel(aUIObj.MainPanelName, CMainPanelLbAnchor, CMainPanelRtAnchor, false, CuiDefaultColors.Background);
            aUIObj.AddLabel(panel, CMainLblTitleLbAnchor, CMainLblTitleRtAnchor, CuiDefaultColors.TextAlt, "Main", string.Empty, 18, TextAnchor.MiddleLeft);
            // Add the ban by ID group
            aUIObj.AddLabel(panel, CMainPageLblBanByIdTitleLbAnchor, CMainPageLblBanByIdTitleRtAnchor, CuiDefaultColors.TextTitle,
                            GetMessage("Ban By ID Title Text", uiUserId), string.Empty, 16, TextAnchor.MiddleLeft);
            aUIObj.AddLabel(panel, CMainPageLblBanByIdLbAnchor, CMainPageLblBanByIdRtAnchor, CuiDefaultColors.TextAlt, GetMessage("Ban By ID Label Text", uiUserId),
                            string.Empty, 14, TextAnchor.MiddleLeft);
            string panelBanByIdGroup = aUIObj.AddPanel(panel, CMainPagePanelBanByIdLbAnchor, CMainPagePanelBanByIdRtAnchor, false, CuiDefaultColors.BackgroundDark);

            if (VerifyPermission(uiUserId, CPermBan)) {
                aUIObj.AddInputField(panelBanByIdGroup, CMainPageEdtBanByIdLbAnchor, CMainPageEdtBanByIdRtAnchor, CuiDefaultColors.TextAlt, string.Empty, 24,
                                     CMainPageBanIdInputTextCmd);
                aUIObj.AddButton(panel, CMainPageBtnBanByIdLbAnchor, CMainPageBtnBanByIdRtAnchor, CuiDefaultColors.ButtonDanger, CuiDefaultColors.TextAlt, "Ban",
                                 CMainPageBanByIdCmd);
            } else {
                aUIObj.AddButton(panel, CMainPageBtnBanByIdLbAnchor, CMainPageBtnBanByIdRtAnchor, CuiDefaultColors.ButtonInactive, CuiDefaultColors.TextAlt, "Ban");
            }

            LogDebug("Built the main page");
        }

        /// <summary>
        /// Build a page of user buttons
        /// </summary>
        /// <param name="aUIObj">Cui object</param>
        /// <param name="aPageType">The active page type</param>
        /// <param name="aPage">User list page</param>
        /// <param name="aIndFiltered">Indicates if the output should be filtered</param>
        private void BuildUserBtnPage(ref Cui aUIObj, UiPage aPageType, int aPage, bool aIndFiltered)
        {
            string uiUserId = aUIObj.GetPlayerId();
            ulong uiUserIdInt = ulong.Parse(uiUserId);
            string pageTitleLabel = GetMessage("User Button Page Title Text", aUIObj.GetPlayerId());
            string npBtnCommandFmt;
            int userCount;

            string panel = aUIObj.AddPanel(aUIObj.MainPanelName, CMainPanelLbAnchor, CMainPanelRtAnchor, false, CuiDefaultColors.Background);
            aUIObj.AddLabel(panel, CUserBtnPageLblTitleLbAnchor, CUserBtnPageLblTitleRtAnchor, CuiDefaultColors.TextAlt, pageTitleLabel, string.Empty, 18,
                            TextAnchor.MiddleLeft);
            // Add search elements
            aUIObj.AddLabel(panel, CUserBtnPageLblSearchLbAnchor, CUserBtnPageLblSearchRtAnchor, CuiDefaultColors.TextAlt, GetMessage("Search Label Text", uiUserId),
                            string.Empty, 16, TextAnchor.MiddleLeft);
            string panelSearchGroup = aUIObj.AddPanel(panel, CUserBtnPagePanelSearchInputLbAnchor, CUserBtnPagePanelSearchInputRtAnchor, false,
                                                      CuiDefaultColors.BackgroundDark);
            aUIObj.AddInputField(panelSearchGroup, CUserBtnPageEdtSearchInputLbAnchor, CUserBtnPageEdtSearchInputRtAnchor, CuiDefaultColors.TextAlt,
                                 (FUserBtnPageSearchInputText.ContainsKey(uiUserIdInt) ? FUserBtnPageSearchInputText[uiUserIdInt] : string.Empty), 100,
                                 CUserBtnPageSearchInputTextCmd, false, string.Empty, 16);

            switch (aPageType) {
                case UiPage.PlayersOnline: {
                    aUIObj.AddButton(panel, CUserBtnPageBtnSearchLbAnchor, CUserBtnPageBtnSearchRtAnchor, CuiDefaultColors.Button, CuiDefaultColors.TextAlt,
                                     GetMessage("Go Button Text", uiUserId), $"{CSwitchUiCmd} {CCmdArgPlayersOnlineSearch} 0", string.Empty, string.Empty, 16);
                    BuildUserButtons(ref aUIObj, panel, aPageType, ref aPage, out npBtnCommandFmt, out userCount, aIndFiltered);
                    break;
                }
                case UiPage.PlayersOffline: {
                    aUIObj.AddButton(panel, CUserBtnPageBtnSearchLbAnchor, CUserBtnPageBtnSearchRtAnchor, CuiDefaultColors.Button, CuiDefaultColors.TextAlt,
                                     GetMessage("Go Button Text", uiUserId), $"{CSwitchUiCmd} {CCmdArgPlayersOfflineSearch} 0", string.Empty, string.Empty, 16);
                    BuildUserButtons(ref aUIObj, panel, aPageType, ref aPage, out npBtnCommandFmt, out userCount, aIndFiltered);
                    break;
                }
                default: {
                    aUIObj.AddButton(panel, CUserBtnPageBtnSearchLbAnchor, CUserBtnPageBtnSearchRtAnchor, CuiDefaultColors.Button, CuiDefaultColors.TextAlt,
                                     GetMessage("Go Button Text", uiUserId), $"{CSwitchUiCmd} {CCmdArgPlayersBannedSearch} 0", string.Empty, string.Empty, 16);
                    BuildBannedUserButtons(ref aUIObj, panel, ref aPage, out npBtnCommandFmt, out userCount, aIndFiltered);
                    break;
                }
            }

            if (aPageType == UiPage.PlayersOnline || aPageType == UiPage.PlayersOffline) {
                BuildUserButtons(ref aUIObj, panel, aPageType, ref aPage, out npBtnCommandFmt, out userCount, aIndFiltered);
            } else {
                BuildBannedUserButtons(ref aUIObj, panel, ref aPage, out npBtnCommandFmt, out userCount, aIndFiltered);
            }

            // Decide whether or not to activate the "previous" button
            if (aPage == 0) {
                aUIObj.AddButton(panel, CUserBtnPageBtnPreviousLbAnchor, CUserBtnPageBtnPreviousRtAnchor, CuiDefaultColors.ButtonInactive, CuiDefaultColors.TextAlt, "<<",
                                 string.Empty, string.Empty, string.Empty, 18);
            } else {
                aUIObj.AddButton(panel, CUserBtnPageBtnPreviousLbAnchor, CUserBtnPageBtnPreviousRtAnchor, CuiDefaultColors.Button, CuiDefaultColors.TextAlt, "<<",
                                 string.Format(npBtnCommandFmt, aPage - 1), string.Empty, string.Empty, 18);
            }

            // Decide whether or not to activate the "next" button
            if (userCount > CMaxPlayerButtons * (aPage + 1)) {
                aUIObj.AddButton(panel, CUserBtnPageBtnNextLbAnchor, CUserBtnPageBtnNextRtAnchor, CuiDefaultColors.Button, CuiDefaultColors.TextAlt, ">>",
                                 string.Format(npBtnCommandFmt, aPage + 1), string.Empty, string.Empty, 18);
            } else {
                aUIObj.AddButton(panel, CUserBtnPageBtnNextLbAnchor, CUserBtnPageBtnNextRtAnchor, CuiDefaultColors.ButtonInactive, CuiDefaultColors.TextAlt, ">>",
                                 string.Empty, string.Empty, string.Empty, 18);
            }

            LogDebug("Built the user button page");
        }

        /// <summary>
        /// Build the current user buttons
        /// </summary>
        /// <param name="aUIObj">Cui object</param>
        /// <param name="aParent">The active page type</param>
        /// <param name="aPageType">The active page type</param>
        /// <param name="aPage">User list page</param>
        /// <param name="aBtnCommandFmt">Command format for the buttons</param>
        /// <param name="aUserCount">Total user count</param>
        /// <param name="aIndFiltered">Indicates if the output should be filtered</param>
        private void BuildUserButtons(ref Cui aUIObj, string aParent, UiPage aPageType, ref int aPage, out string aBtnCommandFmt, out int aUserCount, bool aIndFiltered)
        {
            string commandFmt = $"{CSwitchUiCmd} {CCmdArgPlayerPage} {{0}}";
            List<BasePlayer> userList;

            if (aPageType == UiPage.PlayersOnline) {
                userList = GetServerUserList(aIndFiltered, aUIObj.GetPlayerId());
                aBtnCommandFmt = $"{CSwitchUiCmd} {(aIndFiltered ? CCmdArgPlayersOnlineSearch : CCmdArgPlayersOnline)} {{0}}";
            } else {
                userList = GetServerUserList(aIndFiltered, aUIObj.GetPlayerId(), true);
                aBtnCommandFmt = $"{CSwitchUiCmd} {(aIndFiltered ? CCmdArgPlayersOfflineSearch : CCmdArgPlayersOffline)} {{0}}";
            }

            aUserCount = userList.Count;

            if ((aPage != 0) && (userList.Count <= CMaxPlayerButtons))
                aPage = 0; // Reset page to 0 if user count is lower or equal to max button count

            AddPlayerButtons(ref aUIObj, aParent, ref userList, commandFmt, aPage);
            LogDebug("Built the current page of user buttons");
        }

        /// <summary>
        /// Build the banned user buttons
        /// </summary>
        /// <param name="aUIObj">Cui object</param>
        /// <param name="aParent">The active page type</param>
        /// <param name="aPage">User list page</param>
        /// <param name="aBtnCommandFmt">Command format for the buttons</param>
        /// <param name="aUserCount">Total user count</param>
        /// <param name="aIndFiltered">Indicates if the output should be filtered</param>
        private void BuildBannedUserButtons(ref Cui aUIObj, string aParent, ref int aPage, out string aBtnCommandFmt, out int aUserCount, bool aIndFiltered)
        {
            string commandFmt = $"{CSwitchUiCmd} {CCmdArgPlayerPageBanned} {{0}}";
            List<ServerUsers.User> userList = GetBannedUserList(aIndFiltered, aUIObj.GetPlayerId());
            aBtnCommandFmt = $"{CSwitchUiCmd} {(aIndFiltered ? CCmdArgPlayersBannedSearch : CCmdArgPlayersBanned)} {{0}}";
            aUserCount = userList.Count;

            if ((aPage != 0) && (userList.Count <= CMaxPlayerButtons))
                aPage = 0; // Reset page to 0 if user count is lower or equal to max button count

            AddPlayerButtons(ref aUIObj, aParent, ref userList, commandFmt, aPage);
            LogDebug("Built the current page of banned user buttons");
        }

        /// <summary>
        /// Add the user information labels to the parent element
        /// </summary>
        /// <param name="aUIObj">Cui object</param>
        /// <param name="aParent">Parent panel name</param>
        /// <param name="aUiUserId">UI destination Player ID (SteamId64)</param>
        /// <param name="aPlayerId">Player ID (SteamId64)</param>
        /// <param name="aPlayer">Player who's information we need to display</param>
        private void AddUserPageInfoLabels(ref Cui aUIObj, string aParent, string aUiUserId, ulong aPlayerId, ref BasePlayer aPlayer)
        {
            string lastCheatStr = GetMessage("Never Label Text", aUiUserId);
            string authLevel = ServerUsers.Get(aPlayerId)?.group.ToString() ?? "None";

            // Pre-calc last admin cheat
            if (aPlayer.lastAdminCheatTime > 0f) {
                TimeSpan lastCheatSinceStart = new TimeSpan(0, 0, (int)(Time.realtimeSinceStartup - aPlayer.lastAdminCheatTime));
                lastCheatStr = $"{DateTime.UtcNow.Subtract(lastCheatSinceStart).ToString(@"yyyy\/MM\/dd HH:mm:ss")} UTC";
            }

            aUIObj.AddLabel(aParent, CUserPageLblIdLbAnchor, CUserPageLblIdRtAnchor, CuiDefaultColors.TextAlt, GetMessage("Id Label Format", aUiUserId, aPlayerId,
                                   (aPlayer.IsDeveloper ? GetMessage("Dev Label Text", aUiUserId) : string.Empty)), string.Empty, 14, TextAnchor.MiddleLeft);
            aUIObj.AddLabel(aParent, CUserPageLblAuthLbAnchor, CUserPageLblAuthRtAnchor, CuiDefaultColors.TextAlt,
                            GetMessage("Auth Level Label Format", aUiUserId, authLevel), string.Empty, 14, TextAnchor.MiddleLeft);
            aUIObj.AddLabel(aParent, CUserPageLblConnectLbAnchor, CUserPageLblConnectRtAnchor, CuiDefaultColors.TextAlt,
                            GetMessage("Connection Label Format", aUiUserId, (
                                aPlayer.IsConnected ? GetMessage("Connected Label Text", aUiUserId)
                                                    : GetMessage("Disconnected Label Text", aUiUserId))
                            ), string.Empty, 14, TextAnchor.MiddleLeft);
            aUIObj.AddLabel(aParent, CUserPageLblSleepLbAnchor, CUserPageLblSleepRtAnchor, CuiDefaultColors.TextAlt, GetMessage("Status Label Format", aUiUserId, (
                                aPlayer.IsSleeping() ? GetMessage("Sleeping Label Text", aUiUserId)
                                                    : GetMessage("Awake Label Text", aUiUserId)
                            ), (
                                aPlayer.IsAlive() ? GetMessage("Alive Label Text", aUiUserId)
                                                 : GetMessage("Dead Label Text", aUiUserId))
                            ), string.Empty, 14, TextAnchor.MiddleLeft);
            aUIObj.AddLabel(aParent, CUserPageLblFlagLbAnchor, CUserPageLblFlagRtAnchor, CuiDefaultColors.TextAlt, GetMessage("Flags Label Format", aUiUserId,
                                (aPlayer.IsFlying ? GetMessage("Flying Label Text", aUiUserId) : string.Empty),
                                (aPlayer.isMounted ? GetMessage("Mounted Label Text", aUiUserId) : string.Empty)
                            ), string.Empty, 14, TextAnchor.MiddleLeft);
            aUIObj.AddLabel(aParent, CUserPageLblPosLbAnchor, CUserPageLblPosRtAnchor, CuiDefaultColors.TextAlt,
                            GetMessage("Position Label Format", aUiUserId, aPlayer.ServerPosition), string.Empty, 14, TextAnchor.MiddleLeft);
            aUIObj.AddLabel(aParent, CUserPageLblRotLbAnchor, CUserPageLblRotRtAnchor, CuiDefaultColors.TextAlt,
                            GetMessage("Rotation Label Format", aUiUserId, aPlayer.GetNetworkRotation()), string.Empty, 14, TextAnchor.MiddleLeft);
            aUIObj.AddLabel(aParent, CUserPageLblAdminCheatLbAnchor, CUserPageLblAdminCheatRtAnchor, CuiDefaultColors.TextAlt,
                            GetMessage("Last Admin Cheat Label Format", aUiUserId, lastCheatStr), string.Empty, 14, TextAnchor.MiddleLeft);
            aUIObj.AddLabel(aParent, CUserPageLblIdleLbAnchor, CUserPageLblIdleRtAnchor, CuiDefaultColors.TextAlt,
                            GetMessage("Idle Time Label Format", aUiUserId, Convert.ToInt32(aPlayer.IdleTime)), string.Empty, 14, TextAnchor.MiddleLeft);

            if (Economics != null) {
                double balance = (double)Economics.Call("Balance", aPlayerId);
                aUIObj.AddLabel(aParent, CUserPageLblBalanceLbAnchor, CUserPageLblBalanceRtAnchor, CuiDefaultColors.TextAlt,
                            GetMessage("Economics Balance Label Format", aUiUserId, Convert.ToInt32(balance)), string.Empty, 14, TextAnchor.MiddleLeft);
            }

            if (ServerRewards != null) {
                int points = (int)(ServerRewards.Call("CheckPoints", aPlayerId) ?? 0);
                aUIObj.AddLabel(aParent, CUserPageLblRewardPointsLbAnchor, CUserPageLblRewardPointsRtAnchor, CuiDefaultColors.TextAlt,
                            GetMessage("ServerRewards Points Label Format", aUiUserId, Convert.ToInt32(points)), string.Empty, 14, TextAnchor.MiddleLeft);
            }

            aUIObj.AddLabel(aParent, CUserPageLblHealthLbAnchor, CUserPageLblHealthRtAnchor, CuiDefaultColors.TextAlt,
                            GetMessage("Health Label Format", aUiUserId, aPlayer.health), string.Empty, 14, TextAnchor.MiddleLeft);
            aUIObj.AddLabel(aParent, CUserPageLblCalLbAnchor, CUserPageLblCalRtAnchor, CuiDefaultColors.TextAlt,
                            GetMessage("Calories Label Format", aUiUserId, aPlayer.metabolism?.calories?.value), string.Empty, 14, TextAnchor.MiddleLeft);
            aUIObj.AddLabel(aParent, CUserPageLblHydraLbAnchor, CUserPageLblHydraRtAnchor, CuiDefaultColors.TextAlt,
                            GetMessage("Hydration Label Format", aUiUserId, aPlayer.metabolism?.hydration?.value), string.Empty, 14, TextAnchor.MiddleLeft);
            aUIObj.AddLabel(aParent, CUserPageLblTempLbAnchor, CUserPageLblTempRtAnchor, CuiDefaultColors.TextAlt,
                            GetMessage("Temp Label Format", aUiUserId, aPlayer.metabolism?.temperature?.value), string.Empty, 14, TextAnchor.MiddleLeft);
            aUIObj.AddLabel(aParent, CUserPageLblWetLbAnchor, CUserPageLblWetRtAnchor, CuiDefaultColors.TextAlt,
                            GetMessage("Wetness Label Format", aUiUserId, aPlayer.metabolism?.wetness?.value), string.Empty, 14, TextAnchor.MiddleLeft);
            aUIObj.AddLabel(aParent, CUserPageLblComfortLbAnchor, CUserPageLblComfortRtAnchor, CuiDefaultColors.TextAlt,
                            GetMessage("Comfort Label Format", aUiUserId, aPlayer.metabolism?.comfort?.value), string.Empty, 14, TextAnchor.MiddleLeft);
            aUIObj.AddLabel(aParent, CUserPageLblBleedLbAnchor, CUserPageLblBleedRtAnchor, CuiDefaultColors.TextAlt,
                            GetMessage("Bleeding Label Format", aUiUserId, aPlayer.metabolism?.bleeding?.value), string.Empty, 14, TextAnchor.MiddleLeft);
            aUIObj.AddLabel(aParent, CUserPageLblRads1LbAnchor, CUserPageLblRads1RtAnchor, CuiDefaultColors.TextAlt,
                            GetMessage("Radiation Label Format", aUiUserId, aPlayer.metabolism?.radiation_poison?.value), string.Empty, 14, TextAnchor.MiddleLeft);
            aUIObj.AddLabel(aParent, CUserPageLblRads2LbAnchor, CUserPageLblRads2RtAnchor, CuiDefaultColors.TextAlt,
                            GetMessage("Radiation Protection Label Format", aUiUserId, aPlayer.RadiationProtection()), string.Empty, 14, TextAnchor.MiddleLeft);
        }

        /// <summary>
        /// Add the first row of actions to the parent element
        /// </summary>
        /// <param name="aUIObj">Cui object</param>
        /// <param name="aParent">Parent panel name</param>
        /// <param name="aUiUserId">UI destination Player ID (SteamId64)</param>
        /// <param name="aPlayerId">Player ID (SteamId64)</param>
        /// <param name="aPlayer">Player who's information we need to display</param>
        private void AddUserPageFirstActionRow(ref Cui aUIObj, string aParent, string aUiUserId, ulong aPlayerId, ref BasePlayer aPlayer)
        {
            if (VerifyPermission(aUiUserId, CPermBan)) {
                aUIObj.AddButton(aParent, CUserPageBtnBanLbAnchor, CUserPageBtnBanRtAnchor, CuiDefaultColors.ButtonDanger, CuiDefaultColors.TextAlt,
                                 GetMessage("Ban Button Text", aUiUserId), $"{CBanUserCmd} {aPlayerId}");
            } else {
                aUIObj.AddButton(aParent, CUserPageBtnBanLbAnchor, CUserPageBtnBanRtAnchor, CuiDefaultColors.ButtonInactive, CuiDefaultColors.Text,
                                 GetMessage("Ban Button Text", aUiUserId));
            }

            if (VerifyPermission(aUiUserId, CPermKick) && aPlayer.IsConnected) {
                aUIObj.AddButton(aParent, CUserPageBtnKickLbAnchor, CUserPageBtnKickRtAnchor, CuiDefaultColors.ButtonDanger, CuiDefaultColors.TextAlt,
                                 GetMessage("Kick Button Text", aUiUserId), $"{CKickUserCmd} {aPlayerId}");
            } else {
                aUIObj.AddButton(aParent, CUserPageBtnKickLbAnchor, CUserPageBtnKickRtAnchor, CuiDefaultColors.ButtonInactive, CuiDefaultColors.Text,
                                 GetMessage("Kick Button Text", aUiUserId));
            }
        }

        /// <summary>
        /// Add the second row of actions to the parent element
        /// </summary>
        /// <param name="aUIObj">Cui object</param>
        /// <param name="aParent">Parent panel name</param>
        /// <param name="aUiUserId">UI destination Player ID (SteamId64)</param>
        /// <param name="aPlayerId">Player ID (SteamId64)</param>
        /// <param name="aPlayer">Player who's information we need to display</param>
        private void AddUserPageSecondActionRow(ref Cui aUIObj, string aParent, string aUiUserId, ulong aPlayerId, ref BasePlayer aPlayer)
        {
            bool playerConnected = aPlayer.IsConnected;

            if (VerifyPermission(aUiUserId, CPermVoiceMute) && playerConnected) {
                if (GetIsVoiceMuted(ref aPlayer)) {
                    aUIObj.AddButton(aParent, CUserPageBtnVUnmuteLbAnchor, CUserPageBtnVUnmuteRtAnchor, CuiDefaultColors.ButtonSuccess, CuiDefaultColors.TextAlt,
                                     GetMessage("Voice Unmute Button Text", aUiUserId), $"{CVoiceUnmuteUserCmd} {aPlayerId}");
                    aUIObj.AddButton(aParent, CUserPageBtnVMuteLbAnchor, CUserPageBtnVMuteRtAnchor, CuiDefaultColors.ButtonInactive, CuiDefaultColors.Text,
                                     GetMessage("Voice Mute Button Text", aUiUserId));
                } else {
                    aUIObj.AddButton(aParent, CUserPageBtnVUnmuteLbAnchor, CUserPageBtnVUnmuteRtAnchor, CuiDefaultColors.ButtonInactive, CuiDefaultColors.Text,
                                     GetMessage("Voice Unmute Button Text", aUiUserId));
                    aUIObj.AddButton(aParent, CUserPageBtnVMuteLbAnchor, CUserPageBtnVMuteRtAnchor, CuiDefaultColors.ButtonDanger, CuiDefaultColors.TextAlt,
                                     GetMessage("Voice Mute Button Text", aUiUserId), $"{CVoiceMuteUserCmd} {aPlayerId}");
                }
            } else {
                aUIObj.AddButton(aParent, CUserPageBtnVUnmuteLbAnchor, CUserPageBtnVUnmuteRtAnchor, CuiDefaultColors.ButtonInactive, CuiDefaultColors.Text,
                                 GetMessage("Voice Unmute Button Text", aUiUserId));
                aUIObj.AddButton(aParent, CUserPageBtnVMuteLbAnchor, CUserPageBtnVMuteRtAnchor, CuiDefaultColors.ButtonInactive, CuiDefaultColors.Text,
                                 GetMessage("Voice Mute Button Text", aUiUserId));
            }
        }

        /// <summary>
        /// Add the third row of actions to the parent element
        /// </summary>
        /// <param name="aUIObj">Cui object</param>
        /// <param name="aParent">Parent panel name</param>
        /// <param name="aUiUserId">UI destination Player ID (SteamId64)</param>
        /// <param name="aPlayerId">Player ID (SteamId64)</param>
        /// <param name="aPlayer">Player who's information we need to display</param>
        private void AddUserPageThirdActionRow(ref Cui aUIObj, string aParent, string aUiUserId, ulong aPlayerId, ref BasePlayer aPlayer)
        {
            bool playerConnected = aPlayer.IsConnected;

            if (VerifyPermission(aUiUserId, CPermChatMute) && playerConnected) {
                if (GetIsChatMuted(ref aPlayer)) {
                    aUIObj.AddButton(aParent, CUserPageBtnCUnmuteLbAnchor, CUserPageBtnCUnmuteRtAnchor, CuiDefaultColors.ButtonSuccess, CuiDefaultColors.TextAlt,
                                     GetMessage("Chat Unmute Button Text", aUiUserId), $"{CChatUnmuteUserCmd} {aPlayerId}");
                    aUIObj.AddButton(aParent, CUserPageBtnCMuteLbAnchor, CUserPageBtnCMuteRtAnchor, CuiDefaultColors.ButtonInactive, CuiDefaultColors.Text,
                                     GetMessage("Chat Mute Button Text", aUiUserId));

                    if (BetterChatMute != null) {
                        aUIObj.AddButton(aParent, CUserPageBtnCMuteFifteenLbAnchor, CUserPageBtnCMuteFifteenRtAnchor, CuiDefaultColors.ButtonInactive,
                                         CuiDefaultColors.Text, GetMessage("Chat Mute Button Text 15", aUiUserId));
                        aUIObj.AddButton(aParent, CUserPageBtnCMuteThirtyLbAnchor, CUserPageBtnCMuteThirtyRtAnchor, CuiDefaultColors.ButtonInactive,
                                         CuiDefaultColors.Text, GetMessage("Chat Mute Button Text 30", aUiUserId));
                        aUIObj.AddButton(aParent, CUserPageBtnCMuteSixtyLbAnchor, CUserPageBtnCMuteSixtyRtAnchor, CuiDefaultColors.ButtonInactive,
                                         CuiDefaultColors.Text, GetMessage("Chat Mute Button Text 60", aUiUserId));
                    }
                } else {
                    aUIObj.AddButton(aParent, CUserPageBtnCUnmuteLbAnchor, CUserPageBtnCUnmuteRtAnchor, CuiDefaultColors.ButtonInactive, CuiDefaultColors.Text,
                                     GetMessage("Chat Unmute Button Text", aUiUserId));
                    aUIObj.AddButton(aParent, CUserPageBtnCMuteLbAnchor, CUserPageBtnCMuteRtAnchor, CuiDefaultColors.ButtonDanger, CuiDefaultColors.TextAlt,
                                     GetMessage("Chat Mute Button Text", aUiUserId), $"{CChatMuteUserCmd} {aPlayerId} 0");

                    if (BetterChatMute != null) {
                        aUIObj.AddButton(aParent, CUserPageBtnCMuteFifteenLbAnchor, CUserPageBtnCMuteFifteenRtAnchor, CuiDefaultColors.ButtonDanger,
                                         CuiDefaultColors.TextAlt, GetMessage("Chat Mute Button Text 15", aUiUserId), $"{CChatMuteUserCmd} {aPlayerId} 15");
                        aUIObj.AddButton(aParent, CUserPageBtnCMuteThirtyLbAnchor, CUserPageBtnCMuteThirtyRtAnchor, CuiDefaultColors.ButtonDanger,
                                         CuiDefaultColors.TextAlt, GetMessage("Chat Mute Button Text 30", aUiUserId), $"{CChatMuteUserCmd} {aPlayerId} 30");
                        aUIObj.AddButton(aParent, CUserPageBtnCMuteSixtyLbAnchor, CUserPageBtnCMuteSixtyRtAnchor, CuiDefaultColors.ButtonDanger,
                                         CuiDefaultColors.TextAlt, GetMessage("Chat Mute Button Text 60", aUiUserId), $"{CChatMuteUserCmd} {aPlayerId} 60");
                    }
                }
            } else {
                aUIObj.AddButton(aParent, CUserPageBtnCUnmuteLbAnchor, CUserPageBtnCUnmuteRtAnchor, CuiDefaultColors.ButtonInactive, CuiDefaultColors.Text,
                                 GetMessage("Chat Unmute Button Text", aUiUserId));
                aUIObj.AddButton(aParent, CUserPageBtnCMuteLbAnchor, CUserPageBtnCMuteRtAnchor, CuiDefaultColors.ButtonInactive, CuiDefaultColors.Text,
                                 GetMessage("Chat Mute Button Text", aUiUserId));
            }
        }

        /// <summary>
        /// Add the third row of actions to the parent element
        /// </summary>
        /// <param name="aUIObj">Cui object</param>
        /// <param name="aParent">Parent panel name</param>
        /// <param name="aUiUserId">UI destination Player ID (SteamId64)</param>
        /// <param name="aPlayerId">Player ID (SteamId64)</param>
        /// <param name="aPlayer">Player who's information we need to display</param>
        private void AddUserPageFourthActionRow(ref Cui aUIObj, string aParent, string aUiUserId, ulong aPlayerId, ref BasePlayer aPlayer)
        {
            if (Freeze == null) {
                aUIObj.AddButton(aParent, CUserPageBtnFreezeLbAnchor, CUserPageBtnFreezeRtAnchor, CuiDefaultColors.ButtonInactive, CuiDefaultColors.Text,
                            GetMessage("Freeze Not Installed Button Text", aUiUserId));
            } else if (VerifyPermission(aUiUserId, CPermFreeze) && aPlayer.IsConnected) {
                if (GetIsFrozen(aPlayerId)) {
                    aUIObj.AddButton(aParent, CUserPageBtnUnFreezeLbAnchor, CUserPageBtnUnFreezeRtAnchor, CuiDefaultColors.ButtonSuccess, CuiDefaultColors.TextAlt,
                                GetMessage("UnFreeze Button Text", aUiUserId), $"{CUnreezeCmd} {aPlayerId}");
                    aUIObj.AddButton(aParent, CUserPageBtnFreezeLbAnchor, CUserPageBtnFreezeRtAnchor, CuiDefaultColors.ButtonInactive, CuiDefaultColors.Text,
                                GetMessage("Freeze Button Text", aUiUserId));
                } else {
                    aUIObj.AddButton(aParent, CUserPageBtnUnFreezeLbAnchor, CUserPageBtnUnFreezeRtAnchor, CuiDefaultColors.ButtonInactive, CuiDefaultColors.Text,
                                GetMessage("UnFreeze Button Text", aUiUserId));
                    aUIObj.AddButton(aParent, CUserPageBtnFreezeLbAnchor, CUserPageBtnFreezeRtAnchor, CuiDefaultColors.ButtonDanger, CuiDefaultColors.TextAlt,
                                GetMessage("Freeze Button Text", aUiUserId), $"{CFreezeCmd} {aPlayerId}");
                }
            } else {
                aUIObj.AddButton(aParent, CUserPageBtnUnFreezeLbAnchor, CUserPageBtnUnFreezeRtAnchor, CuiDefaultColors.ButtonInactive, CuiDefaultColors.Text,
                            GetMessage("UnFreeze Button Text", aUiUserId));
                aUIObj.AddButton(aParent, CUserPageBtnFreezeLbAnchor, CUserPageBtnFreezeRtAnchor, CuiDefaultColors.ButtonInactive, CuiDefaultColors.Text,
                            GetMessage("Freeze Button Text", aUiUserId));
            }
        }

        /// <summary>
        /// Add the fourth row of actions to the parent element
        /// </summary>
        /// <param name="aUIObj">Cui object</param>
        /// <param name="aParent">Parent panel name</param>
        /// <param name="aUiUserId">UI destination Player ID (SteamId64)</param>
        /// <param name="aPlayerId">Player ID (SteamId64)</param>
        private void AddUserPageFifthActionRow(ref Cui aUIObj, string aParent, string aUiUserId, ulong aPlayerId)
        {
            if (VerifyPermission(aUiUserId, CPermClearInventory)) {
                aUIObj.AddButton(aParent, CUserPageBtnClearInventoryLbAnchor, CUserPageBtnClearInventoryRtAnchor, CuiDefaultColors.ButtonWarning,
                                 CuiDefaultColors.TextAlt, GetMessage("Clear Inventory Button Text", aUiUserId), $"{CClearUserInventoryCmd} {aPlayerId}");
            } else {
                aUIObj.AddButton(aParent, CUserPageBtnClearInventoryLbAnchor, CUserPageBtnClearInventoryRtAnchor, CuiDefaultColors.ButtonInactive,
                                 CuiDefaultColors.Text, GetMessage("Clear Inventory Button Text", aUiUserId));
            }

            if (VerifyPermission(aUiUserId, CPermResetBP)) {
                aUIObj.AddButton(aParent, CUserPageBtnResetBPLbAnchor, CUserPageBtnResetBPRtAnchor, CuiDefaultColors.ButtonWarning,
                                 CuiDefaultColors.TextAlt, GetMessage("Reset Blueprints Button Text", aUiUserId), $"{CResetUserBPCmd} {aPlayerId}");
            } else {
                aUIObj.AddButton(aParent, CUserPageBtnResetBPLbAnchor, CUserPageBtnResetBPRtAnchor, CuiDefaultColors.ButtonInactive,
                                 CuiDefaultColors.Text, GetMessage("Reset Blueprints Button Text", aUiUserId));
            }

            if (VerifyPermission(aUiUserId, CPermResetMetabolism)) {
                aUIObj.AddButton(aParent, CUserPageBtnResetMetabolismLbAnchor, CUserPageBtnResetMetabolismRtAnchor, CuiDefaultColors.ButtonWarning,
                                 CuiDefaultColors.TextAlt, GetMessage("Reset Metabolism Button Text", aUiUserId), $"{CResetUserMetabolismCmd} {aPlayerId}");
            } else {
                aUIObj.AddButton(aParent, CUserPageBtnResetMetabolismLbAnchor, CUserPageBtnResetMetabolismRtAnchor, CuiDefaultColors.ButtonInactive,
                                 CuiDefaultColors.Text, GetMessage("Reset Metabolism Button Text", aUiUserId));
            }

            if (VerifyPermission(aUiUserId, CPermRecoverMetabolism)) {
                aUIObj.AddButton(aParent, CUserPageBtnRecoverMetabolismLbAnchor, CUserPageBtnRecoverMetabolismRtAnchor, CuiDefaultColors.ButtonWarning,
                                 CuiDefaultColors.TextAlt, GetMessage("Recover Metabolism Button Text", aUiUserId), $"{CRecoverUserMetabolismCmd} {aPlayerId}");
            } else {
                aUIObj.AddButton(aParent, CUserPageBtnRecoverMetabolismLbAnchor, CUserPageBtnRecoverMetabolismRtAnchor, CuiDefaultColors.ButtonInactive,
                                 CuiDefaultColors.Text, GetMessage("Recover Metabolism Button Text", aUiUserId));
            }
        }

        /// <summary>
        /// Add the fifth row of actions to the parent element
        /// </summary>
        /// <param name="aUIObj">Cui object</param>
        /// <param name="aParent">Parent panel name</param>
        /// <param name="aUiUserId">UI destination Player ID (SteamId64)</param>
        /// <param name="aPlayerId">Player ID (SteamId64)</param>
        private void AddUserPageSixthActionRow(ref Cui aUIObj, string aParent, string aUiUserId, ulong aPlayerId)
        {
            if (VerifyPermission(aUiUserId, CPermTeleport)) {
                aUIObj.AddButton(aParent, CUserPageBtnTeleportToLbAnchor, CUserPageBtnTeleportToRtAnchor, CuiDefaultColors.ButtonSuccess,
                                 CuiDefaultColors.TextAlt, GetMessage("Teleport To Player Button Text", aUiUserId), $"{CTeleportToUserCmd} {aPlayerId}");
                aUIObj.AddButton(aParent, CUserPageBtnTeleportLbAnchor, CUserPageBtnTeleportRtAnchor, CuiDefaultColors.ButtonSuccess,
                                 CuiDefaultColors.TextAlt, GetMessage("Teleport Player Button Text", aUiUserId), $"{CTeleportUserCmd} {aPlayerId}");
            } else {
                aUIObj.AddButton(aParent, CUserPageBtnTeleportToLbAnchor, CUserPageBtnTeleportToRtAnchor, CuiDefaultColors.ButtonInactive,
                                 CuiDefaultColors.Text, GetMessage("Teleport To Player Button Text", aUiUserId));
                aUIObj.AddButton(aParent, CUserPageBtnTeleportLbAnchor, CUserPageBtnTeleportRtAnchor, CuiDefaultColors.ButtonInactive,
                                 CuiDefaultColors.Text, GetMessage("Teleport Player Button Text", aUiUserId));
            }

            if (VerifyPermission(aUiUserId, CPermSpectate)) {
                aUIObj.AddButton(aParent, CUserPageBtnSpectateLbAnchor, CUserPageBtnSpectateRtAnchor, CuiDefaultColors.ButtonSuccess,
                                 CuiDefaultColors.TextAlt, GetMessage("Spectate Player Button Text", aUiUserId), $"{CSpectateUserCmd} {aPlayerId}");
            } else {
                aUIObj.AddButton(aParent, CUserPageBtnSpectateLbAnchor, CUserPageBtnSpectateRtAnchor, CuiDefaultColors.ButtonInactive,
                                 CuiDefaultColors.Text, GetMessage("Spectate Player Button Text", aUiUserId));
            }
        }

        /// <summary>
        /// Add the sixth row of actions to the parent element
        /// </summary>
        /// <param name="aUIObj">Cui object</param>
        /// <param name="aParent">Parent panel name</param>
        /// <param name="aUiUserId">UI destination Player ID (SteamId64)</param>
        /// <param name="aPlayerId">Player ID (SteamId64)</param>
        private void AddUserPageSeventhActionRow(ref Cui aUIObj, string aParent, string aUiUserId, ulong aPlayerId)
        {
            if (PermissionsManager == null) {
                aUIObj.AddButton(aParent, CUserPageBtnPermsLbAnchor, CUserPageBtnPermsRtAnchor, CuiDefaultColors.ButtonInactive, CuiDefaultColors.Text,
                                 GetMessage("Perms Not Installed Button Text", aUiUserId));
            } else if (VerifyPermission(aUiUserId, CPermPerms)) {
                aUIObj.AddButton(aParent, CUserPageBtnPermsLbAnchor, CUserPageBtnPermsRtAnchor, CuiDefaultColors.ButtonSuccess, CuiDefaultColors.TextAlt,
                                 GetMessage("Perms Button Text", aUiUserId), $"{CPermsCmd} {aPlayerId}");
            } else {
                aUIObj.AddButton(aParent, CUserPageBtnPermsLbAnchor, CUserPageBtnPermsRtAnchor, CuiDefaultColors.ButtonInactive, CuiDefaultColors.Text,
                                 GetMessage("Perms Button Text", aUiUserId));
            }
        }

        /// <summary>
        /// Add the eleventh row of actions to the parent element
        /// </summary>
        /// <param name="aUIObj">Cui object</param>
        /// <param name="aParent">Parent panel name</param>
        /// <param name="aUiUserId">UI destination Player ID (SteamId64)</param>
        /// <param name="aPlayerId">Player ID (SteamId64)</param>
        private void AddUserPageEleventhActionRow(ref Cui aUIObj, string aParent, string aUiUserId, ulong aPlayerId)
        {
            if (VerifyPermission(aUiUserId, CPermHurt)) {
                aUIObj.AddButton(aParent, CUserPageBtnHurt25LbAnchor, CUserPageBtnHurt25RtAnchor, CuiDefaultColors.ButtonDanger, CuiDefaultColors.TextAlt,
                                 GetMessage("Hurt 25 Button Text", aUiUserId), $"{CHurtUserCmd} {aPlayerId} 25");
                aUIObj.AddButton(aParent, CUserPageBtnHurt50LbAnchor, CUserPageBtnHurt50RtAnchor, CuiDefaultColors.ButtonDanger, CuiDefaultColors.TextAlt,
                                 GetMessage("Hurt 50 Button Text", aUiUserId), $"{CHurtUserCmd} {aPlayerId} 50");
                aUIObj.AddButton(aParent, CUserPageBtnHurt75LbAnchor, CUserPageBtnHurt75RtAnchor, CuiDefaultColors.ButtonDanger, CuiDefaultColors.TextAlt,
                                 GetMessage("Hurt 75 Button Text", aUiUserId), $"{CHurtUserCmd} {aPlayerId} 75");
                aUIObj.AddButton(aParent, CUserPageBtnHurt100LbAnchor, CUserPageBtnHurt100RtAnchor, CuiDefaultColors.ButtonDanger, CuiDefaultColors.TextAlt,
                                 GetMessage("Hurt 100 Button Text", aUiUserId), $"{CHurtUserCmd} {aPlayerId} 100");
            } else {
                aUIObj.AddButton(aParent, CUserPageBtnHurt25LbAnchor, CUserPageBtnHurt25RtAnchor, CuiDefaultColors.ButtonInactive, CuiDefaultColors.Text,
                                 GetMessage("Hurt 25 Button Text", aUiUserId));
                aUIObj.AddButton(aParent, CUserPageBtnHurt50LbAnchor, CUserPageBtnHurt50RtAnchor, CuiDefaultColors.ButtonInactive, CuiDefaultColors.Text,
                                 GetMessage("Hurt 50 Button Text", aUiUserId));
                aUIObj.AddButton(aParent, CUserPageBtnHurt75LbAnchor, CUserPageBtnHurt75RtAnchor, CuiDefaultColors.ButtonInactive, CuiDefaultColors.Text,
                                 GetMessage("Hurt 75 Button Text", aUiUserId));
                aUIObj.AddButton(aParent, CUserPageBtnHurt100LbAnchor, CUserPageBtnHurt100RtAnchor, CuiDefaultColors.ButtonInactive, CuiDefaultColors.Text,
                                 GetMessage("Hurt 100 Button Text", aUiUserId));
            }

            if (VerifyPermission(aUiUserId, CPermKill)) {
                aUIObj.AddButton(aParent, CUserPageBtnKillLbAnchor, CUserPageBtnKillRtAnchor, CuiDefaultColors.ButtonDanger, CuiDefaultColors.TextAlt,
                                 GetMessage("Kill Button Text", aUiUserId), $"{CKillUserCmd} {aPlayerId}");
            } else {
                aUIObj.AddButton(aParent, CUserPageBtnKillLbAnchor, CUserPageBtnKillRtAnchor, CuiDefaultColors.ButtonInactive, CuiDefaultColors.Text,
                                 GetMessage("Kill Button Text", aUiUserId));
            }
        }

        /// <summary>
        /// Add the twelfth row of actions to the parent element
        /// </summary>
        /// <param name="aUIObj">Cui object</param>
        /// <param name="aParent">Parent panel name</param>
        /// <param name="aUiUserId">UI destination Player ID (SteamId64)</param>
        /// <param name="aPlayerId">Player ID (SteamId64)</param>
        private void AddUserPageTwelfthActionRow(ref Cui aUIObj, string aParent, string aUiUserId, ulong aPlayerId)
        {
            if (VerifyPermission(aUiUserId, CPermHeal)) {
                aUIObj.AddButton(aParent, CUserPageBtnHeal25LbAnchor, CUserPageBtnHeal25RtAnchor, CuiDefaultColors.ButtonSuccess, CuiDefaultColors.TextAlt,
                                 GetMessage("Heal 25 Button Text", aUiUserId), $"{CHealUserCmd} {aPlayerId} 25");
                aUIObj.AddButton(aParent, CUserPageBtnHeal50LbAnchor, CUserPageBtnHeal50RtAnchor, CuiDefaultColors.ButtonSuccess, CuiDefaultColors.TextAlt,
                                 GetMessage("Heal 50 Button Text", aUiUserId), $"{CHealUserCmd} {aPlayerId} 50");
                aUIObj.AddButton(aParent, CUserPageBtnHeal75LbAnchor, CUserPageBtnHeal75RtAnchor, CuiDefaultColors.ButtonSuccess, CuiDefaultColors.TextAlt,
                                 GetMessage("Heal 75 Button Text", aUiUserId), $"{CHealUserCmd} {aPlayerId} 75");
                aUIObj.AddButton(aParent, CUserPageBtnHeal100LbAnchor, CUserPageBtnHeal100RtAnchor, CuiDefaultColors.ButtonSuccess, CuiDefaultColors.TextAlt,
                                 GetMessage("Heal 100 Button Text", aUiUserId), $"{CHealUserCmd} {aPlayerId} 100");
                aUIObj.AddButton(aParent, CUserPageBtnHealWoundsLbAnchor, CUserPageBtnHealWoundsRtAnchor, CuiDefaultColors.ButtonSuccess, CuiDefaultColors.TextAlt,
                                 GetMessage("Heal Wounds Button Text", aUiUserId), $"{CHealUserCmd} {aPlayerId} 0");
            } else {
                aUIObj.AddButton(aParent, CUserPageBtnHeal25LbAnchor, CUserPageBtnHeal25RtAnchor, CuiDefaultColors.ButtonInactive, CuiDefaultColors.Text,
                                 GetMessage("Heal 25 Button Text", aUiUserId));
                aUIObj.AddButton(aParent, CUserPageBtnHeal50LbAnchor, CUserPageBtnHeal50RtAnchor, CuiDefaultColors.ButtonInactive, CuiDefaultColors.Text,
                                 GetMessage("Heal 50 Button Text", aUiUserId));
                aUIObj.AddButton(aParent, CUserPageBtnHeal75LbAnchor, CUserPageBtnHeal75RtAnchor, CuiDefaultColors.ButtonInactive, CuiDefaultColors.Text,
                                 GetMessage("Heal 75 Button Text", aUiUserId));
                aUIObj.AddButton(aParent, CUserPageBtnHeal100LbAnchor, CUserPageBtnHeal100RtAnchor, CuiDefaultColors.ButtonInactive, CuiDefaultColors.Text,
                                 GetMessage("Heal 100 Button Text", aUiUserId));
                aUIObj.AddButton(aParent, CUserPageBtnHealWoundsLbAnchor, CUserPageBtnHealWoundsRtAnchor, CuiDefaultColors.ButtonInactive, CuiDefaultColors.Text,
                                 GetMessage("Heal Wounds Button Text", aUiUserId));
            }
        }

        /// <summary>
        /// Build the user information and administration page
        /// This kind of method will always be complex, so ignore metrics about it, please. :)
        /// </summary>
        /// <param name="aUIObj">Cui object</param>
        /// <param name="aPageType">The active page type</param>
        /// <param name="aPlayerId">Player ID (SteamId64)</param>
        private void BuildUserPage(ref Cui aUIObj, UiPage aPageType, ulong aPlayerId)
        {
            string uiUserId = aUIObj.GetPlayerId();
            // Add panels
            string panel = aUIObj.AddPanel(aUIObj.MainPanelName, CMainPanelLbAnchor, CMainPanelRtAnchor, false, CuiDefaultColors.Background);
            string infoPanel = aUIObj.AddPanel(panel, CUserPageInfoPanelLbAnchor, CUserPageInfoPanelRtAnchor, false, CuiDefaultColors.BackgroundMedium);
            string actionPanel = aUIObj.AddPanel(panel, CUserPageActionPanelLbAnchor, CUserPageActionPanelRtAnchor, false, CuiDefaultColors.BackgroundMedium);

            // Add title labels
            aUIObj.AddLabel(infoPanel, CUserPageLblinfoTitleLbAnchor, CUserPageLblinfoTitleRtAnchor, CuiDefaultColors.TextTitle,
                            GetMessage("Player Info Label Text", uiUserId), string.Empty, 14, TextAnchor.MiddleLeft);
            aUIObj.AddLabel(actionPanel, CUserPageLblActionTitleLbAnchor, CUserPageLblActionTitleRtAnchor, CuiDefaultColors.TextTitle,
                            GetMessage("Player Actions Label Text", uiUserId), string.Empty, 14, TextAnchor.MiddleLeft);

            if (aPageType == UiPage.PlayerPage) {
                BasePlayer player = BasePlayer.FindByID(aPlayerId) ?? BasePlayer.FindSleeping(aPlayerId);

                aUIObj.AddLabel(panel, CMainLblTitleLbAnchor, CMainLblTitleRtAnchor, CuiDefaultColors.TextAlt,
                                GetMessage("User Page Title Format", uiUserId, player.displayName, string.Empty), string.Empty, 18, TextAnchor.MiddleLeft);
                // Add user info labels
                AddUserPageInfoLabels(ref aUIObj, infoPanel, uiUserId, aPlayerId, ref player);

                // --- Build player action panel
                // Ban, Kick
                AddUserPageFirstActionRow(ref aUIObj, actionPanel, uiUserId, aPlayerId, ref player);
                // Unmute voice, Mute voice
                AddUserPageSecondActionRow(ref aUIObj, actionPanel, uiUserId, aPlayerId, ref player);
                // Unmute chat, Mute chat (And timed ones if BetterChat is available)
                AddUserPageThirdActionRow(ref aUIObj, actionPanel, uiUserId, aPlayerId, ref player);
                // Unfreeze, Freeze
                AddUserPageFourthActionRow(ref aUIObj, actionPanel, uiUserId, aPlayerId, ref player);
                // Clear inventory, Reset BP, Reset metabolism
                AddUserPageFifthActionRow(ref aUIObj, actionPanel, uiUserId, aPlayerId);
                // Teleport to, Teleport, Spectate
                AddUserPageSixthActionRow(ref aUIObj, actionPanel, uiUserId, aPlayerId);
                // Perms
                AddUserPageSeventhActionRow(ref aUIObj, actionPanel, uiUserId, aPlayerId);
                // Hurt 25, Hurt 50, Hurt 75, Hurt 100, Kill
                AddUserPageEleventhActionRow(ref aUIObj, actionPanel, uiUserId, aPlayerId);
                // Heal 25, Heal 50, Heal 75, Heal 100, Heal wounds
                AddUserPageTwelfthActionRow(ref aUIObj, actionPanel, uiUserId, aPlayerId);
            } else {
                ServerUsers.User serverUser = ServerUsers.Get(aPlayerId);
                aUIObj.AddLabel(panel, CMainLblTitleLbAnchor, CMainLblTitleRtAnchor, CuiDefaultColors.TextAlt,
                                GetMessage("User Page Title Format", uiUserId, serverUser.username, GetMessage("Banned Label Text", uiUserId)),
                                string.Empty, 18, TextAnchor.MiddleLeft);
                // Add user info labels
                aUIObj.AddLabel(infoPanel, CUserPageLblIdLbAnchor, CUserPageLblIdRtAnchor, CuiDefaultColors.TextAlt,
                                GetMessage("Id Label Format", uiUserId, aPlayerId, string.Empty), string.Empty, 14, TextAnchor.MiddleLeft);

                // --- Build player action panel
                if (VerifyPermission(uiUserId, CPermBan)) {
                    aUIObj.AddButton(actionPanel, CUserPageBtnBanLbAnchor, CUserPageBtnBanRtAnchor, CuiDefaultColors.Button, CuiDefaultColors.TextAlt,
                                     GetMessage("Unban Button Text", uiUserId), $"{CUnbanUserCmd} {aPlayerId}");
                } else {
                    aUIObj.AddButton(actionPanel, CUserPageBtnBanLbAnchor, CUserPageBtnBanRtAnchor, CuiDefaultColors.ButtonInactive, CuiDefaultColors.Text,
                                     GetMessage("Unban Button Text", uiUserId));
                }
            }

            LogDebug("Built user information page");
        }

        /// <summary>
        /// Initiate the building of the UI page to show
        /// </summary>
        /// <param name="aPlayer">UI destination player</param>
        /// <param name="aPageType">Type of the page</param>
        /// <param name="aArg">Argument</param>
        /// <param name="aIndFiltered">Indicates if the output should be filtered</param>
        private void BuildUI(BasePlayer aPlayer, UiPage aPageType, string aArg = "", bool aIndFiltered = false)
        {
            // Initiate the new UI and panel
            Cui newUiLib = new Cui(aPlayer);
            newUiLib.MainPanelName = newUiLib.AddPanel(Cui.ParentOverlay, CMainLbAnchor, CMainRtAnchor, true, CuiDefaultColors.BackgroundDark, CMainPanelName);
            BuildTabMenu(ref newUiLib, aPageType);

            switch (aPageType) {
                case UiPage.Main: {
                    BuildMainPage(ref newUiLib);
                    break;
                }
                case UiPage.PlayersOnline:
                case UiPage.PlayersOffline:
                case UiPage.PlayersBanned: {
                    int page = 0;

                    if (!string.IsNullOrEmpty(aArg))
                        if (!int.TryParse(aArg, out page))
                            page = 0; // Just to be sure

                    BuildUserBtnPage(ref newUiLib, aPageType, page, aIndFiltered);
                    break;
                }
                case UiPage.PlayerPage:
                case UiPage.PlayerPageBanned: {
                    ulong playerId = aPlayer.userID;

                    if (!string.IsNullOrEmpty(aArg))
                        if (!ulong.TryParse(aArg, out playerId))
                            playerId = aPlayer.userID; // Just to be sure

                    BuildUserPage(ref newUiLib, aPageType, playerId);
                    break;
                }
            }

            // Cleanup any old/active UI and draw the new one
            CuiHelper.DestroyUi(aPlayer, CMainPanelName);
            newUiLib.Draw();
        }
        #endregion GUI build methods

        #region Config
        /// <summary>
        /// The config type class
        /// </summary>
        private class ConfigData
        {
            [DefaultValue(true)]
            [JsonProperty("Use Permission System", DefaultValueHandling = DefaultValueHandling.Populate)]
            public bool UsePermSystem { get; set; }
            [DefaultValue("")]
            [JsonProperty("Discord Webhook url for ban messages", DefaultValueHandling = DefaultValueHandling.Populate)]
            public string BanMsgWebhookUrl { get; set; }
            [DefaultValue("")]
            [JsonProperty("Discord Webhook url for kick messages", DefaultValueHandling = DefaultValueHandling.Populate)]
            public string KickMsgWebhookUrl { get; set; }
        }
        #endregion

        #region Constants
        private readonly bool CDebugEnabled = false;
        private const int CMaxPlayerCols = 5;
        private const int CMaxPlayerRows = 12;
        private const int CMaxPlayerButtons = CMaxPlayerCols * CMaxPlayerRows;
        private const string CMainPanelName = "PAdm_MainPanel";
        private readonly List<string> CUnknownNameList = new List<string> { "unnamed", "unknown" };
        #region Local commands
        private const string CPadminCmd = "padmin";
        private const string CCloseUiCmd = "padm_closeui";
        private const string CSwitchUiCmd = "padm_switchui";
        private const string CKickUserCmd = "padm_kickuser";
        private const string CBanUserCmd = "padm_banuser";
        private const string CMainPageBanByIdCmd = "padm_mainpagebanbyid";
        private const string CUnbanUserCmd = "padm_unbanuser";
        private const string CPermsCmd = "padm_perms";
        private const string CVoiceMuteUserCmd = "padm_vmuteuser";
        private const string CVoiceUnmuteUserCmd = "padm_vunmuteuser";
        private const string CChatMuteUserCmd = "padm_cmuteuser";
        private const string CChatUnmuteUserCmd = "padm_cunmuteuser";
        private const string CFreezeCmd = "padm_freeze";
        private const string CUnreezeCmd = "padm_unfreeze";
        private const string CClearUserInventoryCmd = "padm_clearuserinventory";
        private const string CResetUserBPCmd = "padm_resetuserblueprints";
        private const string CResetUserMetabolismCmd = "padm_resetusermetabolism";
        private const string CRecoverUserMetabolismCmd = "padm_recoverusermetabolism";
        private const string CHurtUserCmd = "padm_hurtuser";
        private const string CKillUserCmd = "padm_killuser";
        private const string CHealUserCmd = "padm_healuser";
        private const string CTeleportToUserCmd = "padm_tptouser";
        private const string CTeleportUserCmd = "padm_tpuser";
        private const string CSpectateUserCmd = "padm_spectateuser";
        private const string CMainPageBanIdInputTextCmd = "padm_mainpagebanidinputtext";
        private const string CUserBtnPageSearchInputTextCmd = "padm_userbtnpagesearchinputtext";
        #endregion Local commands
        #region Foreign commands
        private const string CPermsPermsCmd = "perms player";
        private const string CFreezeFreezeCmd = "freeze";
        private const string CFreezeUnfreezeCmd = "unfreeze";
        #endregion Foreign commands
        #region Local Command Static Arguments
        private const string CCmdArgMain = "main";
        private const string CCmdArgPlayersOnline = "playersonline";
        private const string CCmdArgPlayersOnlineSearch = "playersonlinesearch";
        private const string CCmdArgPlayersOffline = "playersoffline";
        private const string CCmdArgPlayersOfflineSearch = "playersofflinesearch";
        private const string CCmdArgPlayersBanned = "playersbanned";
        private const string CCmdArgPlayersBannedSearch = "playersbannedsearch";
        private const string CCmdArgPlayerPage = "playerpage";
        private const string CCmdArgPlayerPageBanned = "playerpagebanned";
        #endregion Local Command Static Arguments
        #region Local permissions
        private const string CPermUiShow = "playeradministration.show";
        private const string CPermKick = "playeradministration.kick";
        private const string CPermBan = "playeradministration.ban";
        private const string CPermKill = "playeradministration.kill";
        private const string CPermPerms = "playeradministration.perms";
        private const string CPermVoiceMute = "playeradministration.voicemute";
        private const string CPermChatMute = "playeradministration.chatmute";
        private const string CPermFreeze = "playeradministration.freeze";
        private const string CPermClearInventory = "playeradministration.clearinventory";
        private const string CPermResetBP = "playeradministration.resetblueprint";
        private const string CPermResetMetabolism = "playeradministration.resetmetabolism";
        private const string CPermRecoverMetabolism = "playeradministration.recovermetabolism";
        private const string CPermHurt = "playeradministration.hurt";
        private const string CPermHeal = "playeradministration.heal";
        private const string CPermTeleport = "playeradministration.teleport";
        private const string CPermSpectate = "playeradministration.spectate";
        #endregion Local permissions
        #region Foreign permissions
        private const string CPermFreezeFrozen = "freeze.frozen";
        #endregion Foreign permissions
        /* Define layout */
        #region Main bounds
        private readonly CuiPoint CMainLbAnchor = new CuiPoint(0.03f, 0.15f);
        private readonly CuiPoint CMainRtAnchor = new CuiPoint(0.97f, 0.97f);
        private readonly CuiPoint CMainMenuHeaderContainerLbAnchor = new CuiPoint(0.005f, 0.937f);
        private readonly CuiPoint CMainMenuHeaderContainerRtAnchor = new CuiPoint(0.995f, 0.99f);
        private readonly CuiPoint CMainMenuTabBtnContainerLbAnchor = new CuiPoint(0.005f, 0.867f);
        private readonly CuiPoint CMainMenuTabBtnContainerRtAnchor = new CuiPoint(0.995f, 0.927f);
        private readonly CuiPoint CMainMenuHeaderLblLbAnchor = new CuiPoint(0f, 0f);
        private readonly CuiPoint CMainMenuHeaderLblRtAnchor = new CuiPoint(1f, 1f);
        private readonly CuiPoint CMainMenuCloseBtnLbAnchor = new CuiPoint(0.965f, 0f);
        private readonly CuiPoint CMainMenuCloseBtnRtAnchor = new CuiPoint(1f, 1f);
        private readonly CuiPoint CMainPanelLbAnchor = new CuiPoint(0.005f, 0.01f);
        private readonly CuiPoint CMainPanelRtAnchor = new CuiPoint(0.995f, 0.857f);
        private readonly CuiPoint CMainLblTitleLbAnchor = new CuiPoint(0.005f, 0.93f);
        private readonly CuiPoint CMainLblTitleRtAnchor = new CuiPoint(0.995f, 0.99f);
        #endregion Main bounds
        #region Main page bounds
        private readonly CuiPoint CMainPageLblBanByIdTitleLbAnchor = new CuiPoint(0.005f, 0.84f);
        private readonly CuiPoint CMainPageLblBanByIdTitleRtAnchor = new CuiPoint(0.995f, 0.89f);
        private readonly CuiPoint CMainPageLblBanByIdLbAnchor = new CuiPoint(0.005f, 0.76f);
        private readonly CuiPoint CMainPageLblBanByIdRtAnchor = new CuiPoint(0.05f, 0.81f);
        private readonly CuiPoint CMainPagePanelBanByIdLbAnchor = new CuiPoint(0.055f, 0.76f);
        private readonly CuiPoint CMainPagePanelBanByIdRtAnchor = new CuiPoint(0.305f, 0.81f);
        private readonly CuiPoint CMainPageEdtBanByIdLbAnchor = new CuiPoint(0.005f, 0f);
        private readonly CuiPoint CMainPageEdtBanByIdRtAnchor = new CuiPoint(0.995f, 1f);
        private readonly CuiPoint CMainPageBtnBanByIdLbAnchor = new CuiPoint(0.315f, 0.76f);
        private readonly CuiPoint CMainPageBtnBanByIdRtAnchor = new CuiPoint(0.365f, 0.81f);
        #endregion Main page bounds
        #region User button page bounds
        private readonly CuiPoint CUserBtnPageLblTitleLbAnchor = new CuiPoint(0.005f, 0.93f);
        private readonly CuiPoint CUserBtnPageLblTitleRtAnchor = new CuiPoint(0.495f, 0.99f);
        private readonly CuiPoint CUserBtnPageLblSearchLbAnchor = new CuiPoint(0.52f, 0.93f);
        private readonly CuiPoint CUserBtnPageLblSearchRtAnchor = new CuiPoint(0.565f, 0.99f);
        private readonly CuiPoint CUserBtnPagePanelSearchInputLbAnchor = new CuiPoint(0.57f, 0.94f);
        private readonly CuiPoint CUserBtnPagePanelSearchInputRtAnchor = new CuiPoint(0.945f, 0.99f);
        private readonly CuiPoint CUserBtnPageEdtSearchInputLbAnchor = new CuiPoint(0.005f, 0f);
        private readonly CuiPoint CUserBtnPageEdtSearchInputRtAnchor = new CuiPoint(0.995f, 1f);
        private readonly CuiPoint CUserBtnPageBtnSearchLbAnchor = new CuiPoint(0.95f, 0.94f);
        private readonly CuiPoint CUserBtnPageBtnSearchRtAnchor = new CuiPoint(0.995f, 0.99f);
        private readonly CuiPoint CUserBtnPageBtnPreviousLbAnchor = new CuiPoint(0.005f, 0.01f);
        private readonly CuiPoint CUserBtnPageBtnPreviousRtAnchor = new CuiPoint(0.035f, 0.06f);
        private readonly CuiPoint CUserBtnPageBtnNextLbAnchor = new CuiPoint(0.96f, 0.01f);
        private readonly CuiPoint CUserBtnPageBtnNextRtAnchor = new CuiPoint(0.995f, 0.06f);
        #endregion User button page bounds
        #region User page panel bounds
        private readonly CuiPoint CUserPageInfoPanelLbAnchor = new CuiPoint(0.005f, 0.01f);
        private readonly CuiPoint CUserPageInfoPanelRtAnchor = new CuiPoint(0.28f, 0.92f);
        private readonly CuiPoint CUserPageActionPanelLbAnchor = new CuiPoint(0.285f, 0.01f);
        private readonly CuiPoint CUserPageActionPanelRtAnchor = new CuiPoint(0.995f, 0.92f);
        #region User page title label bounds
        private readonly CuiPoint CUserPageLblinfoTitleLbAnchor = new CuiPoint(0.025f, 0.94f);
        private readonly CuiPoint CUserPageLblinfoTitleRtAnchor = new CuiPoint(0.975f, 0.99f);
        private readonly CuiPoint CUserPageLblActionTitleLbAnchor = new CuiPoint(0.01f, 0.94f);
        private readonly CuiPoint CUserPageLblActionTitleRtAnchor = new CuiPoint(0.99f, 0.99f);
        #endregion User page title label bounds
        #region User page info label bounds
        // Top part
        private readonly CuiPoint CUserPageLblIdLbAnchor = new CuiPoint(0.025f, 0.88f);
        private readonly CuiPoint CUserPageLblIdRtAnchor = new CuiPoint(0.975f, 0.92f);
        private readonly CuiPoint CUserPageLblAuthLbAnchor = new CuiPoint(0.025f, 0.835f);
        private readonly CuiPoint CUserPageLblAuthRtAnchor = new CuiPoint(0.975f, 0.875f);
        private readonly CuiPoint CUserPageLblConnectLbAnchor = new CuiPoint(0.025f, 0.79f);
        private readonly CuiPoint CUserPageLblConnectRtAnchor = new CuiPoint(0.975f, 0.83f);
        private readonly CuiPoint CUserPageLblSleepLbAnchor = new CuiPoint(0.025f, 0.745f);
        private readonly CuiPoint CUserPageLblSleepRtAnchor = new CuiPoint(0.975f, 0.785f);
        private readonly CuiPoint CUserPageLblFlagLbAnchor = new CuiPoint(0.025f, 0.70f);
        private readonly CuiPoint CUserPageLblFlagRtAnchor = new CuiPoint(0.975f, 0.74f);
        private readonly CuiPoint CUserPageLblPosLbAnchor = new CuiPoint(0.025f, 0.655f);
        private readonly CuiPoint CUserPageLblPosRtAnchor = new CuiPoint(0.975f, 0.695f);
        private readonly CuiPoint CUserPageLblRotLbAnchor = new CuiPoint(0.025f, 0.61f);
        private readonly CuiPoint CUserPageLblRotRtAnchor = new CuiPoint(0.975f, 0.65f);
        private readonly CuiPoint CUserPageLblAdminCheatLbAnchor = new CuiPoint(0.025f, 0.555f);
        private readonly CuiPoint CUserPageLblAdminCheatRtAnchor = new CuiPoint(0.975f, 0.605f);
        private readonly CuiPoint CUserPageLblIdleLbAnchor = new CuiPoint(0.025f, 0.51f);
        private readonly CuiPoint CUserPageLblIdleRtAnchor = new CuiPoint(0.975f, 0.55f);
        private readonly CuiPoint CUserPageLblBalanceLbAnchor = new CuiPoint(0.025f, 0.465f);
        private readonly CuiPoint CUserPageLblBalanceRtAnchor = new CuiPoint(0.975f, 0.505f);
        private readonly CuiPoint CUserPageLblRewardPointsLbAnchor = new CuiPoint(0.025f, 0.42f);
        private readonly CuiPoint CUserPageLblRewardPointsRtAnchor = new CuiPoint(0.975f, 0.46f);
        // Bottom part
        private readonly CuiPoint CUserPageLblHealthLbAnchor = new CuiPoint(0.025f, 0.195f);
        private readonly CuiPoint CUserPageLblHealthRtAnchor = new CuiPoint(0.975f, 0.235f);
        private readonly CuiPoint CUserPageLblCalLbAnchor = new CuiPoint(0.025f, 0.145f);
        private readonly CuiPoint CUserPageLblCalRtAnchor = new CuiPoint(0.5f, 0.19f);
        private readonly CuiPoint CUserPageLblHydraLbAnchor = new CuiPoint(0.5f, 0.145f);
        private readonly CuiPoint CUserPageLblHydraRtAnchor = new CuiPoint(0.975f, 0.19f);
        private readonly CuiPoint CUserPageLblTempLbAnchor = new CuiPoint(0.025f, 0.10f);
        private readonly CuiPoint CUserPageLblTempRtAnchor = new CuiPoint(0.5f, 0.14f);
        private readonly CuiPoint CUserPageLblWetLbAnchor = new CuiPoint(0.5f, 0.10f);
        private readonly CuiPoint CUserPageLblWetRtAnchor = new CuiPoint(0.975f, 0.14f);
        private readonly CuiPoint CUserPageLblComfortLbAnchor = new CuiPoint(0.025f, 0.055f);
        private readonly CuiPoint CUserPageLblComfortRtAnchor = new CuiPoint(0.5f, 0.095f);
        private readonly CuiPoint CUserPageLblBleedLbAnchor = new CuiPoint(0.5f, 0.055f);
        private readonly CuiPoint CUserPageLblBleedRtAnchor = new CuiPoint(0.975f, 0.095f);
        private readonly CuiPoint CUserPageLblRads1LbAnchor = new CuiPoint(0.025f, 0.01f);
        private readonly CuiPoint CUserPageLblRads1RtAnchor = new CuiPoint(0.5f, 0.05f);
        private readonly CuiPoint CUserPageLblRads2LbAnchor = new CuiPoint(0.5f, 0.01f);
        private readonly CuiPoint CUserPageLblRads2RtAnchor = new CuiPoint(0.975f, 0.05f);
        #endregion User page info label bounds
        #region User page button bounds
        // Row 1
        private readonly CuiPoint CUserPageBtnBanLbAnchor = new CuiPoint(0.01f, 0.86f);
        private readonly CuiPoint CUserPageBtnBanRtAnchor = new CuiPoint(0.16f, 0.92f);
        private readonly CuiPoint CUserPageBtnKickLbAnchor = new CuiPoint(0.17f, 0.86f);
        private readonly CuiPoint CUserPageBtnKickRtAnchor = new CuiPoint(0.32f, 0.92f);
        // Row 2
        private readonly CuiPoint CUserPageBtnVUnmuteLbAnchor = new CuiPoint(0.01f, 0.78f);
        private readonly CuiPoint CUserPageBtnVUnmuteRtAnchor = new CuiPoint(0.16f, 0.84f);
        private readonly CuiPoint CUserPageBtnVMuteLbAnchor = new CuiPoint(0.17f, 0.78f);
        private readonly CuiPoint CUserPageBtnVMuteRtAnchor = new CuiPoint(0.32f, 0.84f);
        // Row 3
        private readonly CuiPoint CUserPageBtnCUnmuteLbAnchor = new CuiPoint(0.01f, 0.70f);
        private readonly CuiPoint CUserPageBtnCUnmuteRtAnchor = new CuiPoint(0.16f, 0.76f);
        private readonly CuiPoint CUserPageBtnCMuteLbAnchor = new CuiPoint(0.17f, 0.70f);
        private readonly CuiPoint CUserPageBtnCMuteRtAnchor = new CuiPoint(0.32f, 0.76f);
        private readonly CuiPoint CUserPageBtnCMuteFifteenLbAnchor = new CuiPoint(0.33f, 0.70f);
        private readonly CuiPoint CUserPageBtnCMuteFifteenRtAnchor = new CuiPoint(0.48f, 0.76f);
        private readonly CuiPoint CUserPageBtnCMuteThirtyLbAnchor = new CuiPoint(0.49f, 0.70f);
        private readonly CuiPoint CUserPageBtnCMuteThirtyRtAnchor = new CuiPoint(0.64f, 0.76f);
        private readonly CuiPoint CUserPageBtnCMuteSixtyLbAnchor = new CuiPoint(0.65f, 0.70f);
        private readonly CuiPoint CUserPageBtnCMuteSixtyRtAnchor = new CuiPoint(0.80f, 0.76f);
        // Row 4
        private readonly CuiPoint CUserPageBtnUnFreezeLbAnchor = new CuiPoint(0.01f, 0.62f);
        private readonly CuiPoint CUserPageBtnUnFreezeRtAnchor = new CuiPoint(0.16f, 0.68f);
        private readonly CuiPoint CUserPageBtnFreezeLbAnchor = new CuiPoint(0.17f, 0.62f);
        private readonly CuiPoint CUserPageBtnFreezeRtAnchor = new CuiPoint(0.32f, 0.68f);
        // Row 5
        private readonly CuiPoint CUserPageBtnClearInventoryLbAnchor = new CuiPoint(0.01f, 0.54f);
        private readonly CuiPoint CUserPageBtnClearInventoryRtAnchor = new CuiPoint(0.16f, 0.60f);
        private readonly CuiPoint CUserPageBtnResetBPLbAnchor = new CuiPoint(0.17f, 0.54f);
        private readonly CuiPoint CUserPageBtnResetBPRtAnchor = new CuiPoint(0.32f, 0.60f);
        private readonly CuiPoint CUserPageBtnResetMetabolismLbAnchor = new CuiPoint(0.33f, 0.54f);
        private readonly CuiPoint CUserPageBtnResetMetabolismRtAnchor = new CuiPoint(0.48f, 0.60f);
        private readonly CuiPoint CUserPageBtnRecoverMetabolismLbAnchor = new CuiPoint(0.49f, 0.54f);
        private readonly CuiPoint CUserPageBtnRecoverMetabolismRtAnchor = new CuiPoint(0.64f, 0.60f);
        // Row 6
        private readonly CuiPoint CUserPageBtnTeleportToLbAnchor = new CuiPoint(0.01f, 0.46f);
        private readonly CuiPoint CUserPageBtnTeleportToRtAnchor = new CuiPoint(0.16f, 0.52f);
        private readonly CuiPoint CUserPageBtnTeleportLbAnchor = new CuiPoint(0.17f, 0.46f);
        private readonly CuiPoint CUserPageBtnTeleportRtAnchor = new CuiPoint(0.32f, 0.52f);
        private readonly CuiPoint CUserPageBtnSpectateLbAnchor = new CuiPoint(0.33f, 0.46f);
        private readonly CuiPoint CUserPageBtnSpectateRtAnchor = new CuiPoint(0.48f, 0.52f);
        // Row 7
        private readonly CuiPoint CUserPageBtnPermsLbAnchor = new CuiPoint(0.01f, 0.38f);
        private readonly CuiPoint CUserPageBtnPermsRtAnchor = new CuiPoint(0.16f, 0.44f);
        // Row 11
        private readonly CuiPoint CUserPageBtnHurt25LbAnchor = new CuiPoint(0.01f, 0.10f);
        private readonly CuiPoint CUserPageBtnHurt25RtAnchor = new CuiPoint(0.16f, 0.16f);
        private readonly CuiPoint CUserPageBtnHurt50LbAnchor = new CuiPoint(0.17f, 0.10f);
        private readonly CuiPoint CUserPageBtnHurt50RtAnchor = new CuiPoint(0.32f, 0.16f);
        private readonly CuiPoint CUserPageBtnHurt75LbAnchor = new CuiPoint(0.33f, 0.10f);
        private readonly CuiPoint CUserPageBtnHurt75RtAnchor = new CuiPoint(0.48f, 0.16f);
        private readonly CuiPoint CUserPageBtnHurt100LbAnchor = new CuiPoint(0.49f, 0.10f);
        private readonly CuiPoint CUserPageBtnHurt100RtAnchor = new CuiPoint(0.64f, 0.16f);
        private readonly CuiPoint CUserPageBtnKillLbAnchor = new CuiPoint(0.65f, 0.10f);
        private readonly CuiPoint CUserPageBtnKillRtAnchor = new CuiPoint(0.80f, 0.16f);
        // Row 12
        private readonly CuiPoint CUserPageBtnHeal25LbAnchor = new CuiPoint(0.01f, 0.02f);
        private readonly CuiPoint CUserPageBtnHeal25RtAnchor = new CuiPoint(0.16f, 0.08f);
        private readonly CuiPoint CUserPageBtnHeal50LbAnchor = new CuiPoint(0.17f, 0.02f);
        private readonly CuiPoint CUserPageBtnHeal50RtAnchor = new CuiPoint(0.32f, 0.08f);
        private readonly CuiPoint CUserPageBtnHeal75LbAnchor = new CuiPoint(0.33f, 0.02f);
        private readonly CuiPoint CUserPageBtnHeal75RtAnchor = new CuiPoint(0.48f, 0.08f);
        private readonly CuiPoint CUserPageBtnHeal100LbAnchor = new CuiPoint(0.49f, 0.02f);
        private readonly CuiPoint CUserPageBtnHeal100RtAnchor = new CuiPoint(0.64f, 0.08f);
        private readonly CuiPoint CUserPageBtnHealWoundsLbAnchor = new CuiPoint(0.65f, 0.02f);
        private readonly CuiPoint CUserPageBtnHealWoundsRtAnchor = new CuiPoint(0.80f, 0.08f);
        #endregion User page button bounds
        #endregion User page panel bounds
        #endregion Constants

        #region Variables
        private static PlayerAdministration FPluginInstance;
        private ConfigData FConfigData;
        private Dictionary<ulong, string> FMainPageBanIdInputText = new Dictionary<ulong, string>(); // Format: <userId, text>
        private Dictionary<ulong, string> FUserBtnPageSearchInputText = new Dictionary<ulong, string>(); // Format: <userId, text>
        #endregion Variables

        #region Hooks
        void Loaded()
        {
            LoadConfig();
            permission.RegisterPermission(CPermUiShow, this);
            permission.RegisterPermission(CPermKick, this);
            permission.RegisterPermission(CPermBan, this);
            permission.RegisterPermission(CPermKill, this);
            permission.RegisterPermission(CPermPerms, this);
            permission.RegisterPermission(CPermVoiceMute, this);
            permission.RegisterPermission(CPermChatMute, this);
            permission.RegisterPermission(CPermFreeze, this);
            permission.RegisterPermission(CPermClearInventory, this);
            permission.RegisterPermission(CPermResetBP, this);
            permission.RegisterPermission(CPermResetMetabolism, this);
            permission.RegisterPermission(CPermRecoverMetabolism, this);
            permission.RegisterPermission(CPermHurt, this);
            permission.RegisterPermission(CPermHeal, this);
            permission.RegisterPermission(CPermTeleport, this);
            permission.RegisterPermission(CPermSpectate, this);
            FPluginInstance = this;
        }

        void Unload()
        {
            foreach (BasePlayer player in Player.Players) {
                CuiHelper.DestroyUi(player, CMainPanelName);

                if (FMainPageBanIdInputText.ContainsKey(player.userID))
                    FMainPageBanIdInputText.Remove(player.userID);
            }

            FPluginInstance = null;
        }

        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (FMainPageBanIdInputText.ContainsKey(player.userID))
                FMainPageBanIdInputText.Remove(player.userID);
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();

            try {
                FConfigData = Config.ReadObject<ConfigData>();

                if (FConfigData == null)
                    LoadDefaultConfig();

                if (UpgradeTo1310())
                    LogDebug("Upgraded the config to version 1.3.10");

                if (UpgradeTo1313())
                    LogDebug("Upgraded the config to version 1.3.13");
            } catch {
                LoadDefaultConfig();
            }

            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            FConfigData = new ConfigData {
                UsePermSystem = true,
                BanMsgWebhookUrl = "",
                KickMsgWebhookUrl = ""
            };
            LogDebug("Default config loaded");
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string> {
                { "Permission Error Text", "You do not have the required permissions to use this command." },
                { "Permission Error Log Text", "{0}: Tried to execute a command requiring the '{1}' permission" },
                { "Kick Reason Message Text", "Administrative decision" },
                { "Ban Reason Message Text", "Administrative decision" },

                { "Never Label Text", "Never" },
                { "Banned Label Text", " (Banned)" },
                { "Dev Label Text", " (Developer)" },
                { "Connected Label Text", "Connected" },
                { "Disconnected Label Text", "Disconnected" },
                { "Sleeping Label Text", "Sleeping" },
                { "Awake Label Text", "Awake" },
                { "Alive Label Text", "Alive" },
                { "Dead Label Text", "Dead" },
                { "Flying Label Text", " Flying" },
                { "Mounted Label Text", " Mounted" },

                { "User Button Page Title Text", "Click a username to go to the player's control page" },
                { "User Page Title Format", "Control page for player '{0}'{1}" },

                { "Ban By ID Title Text", "Ban a user by ID" },
                { "Ban By ID Label Text", "User ID:" },
                { "Search Label Text", "Search:" },
                { "Player Info Label Text", "Player information:" },
                { "Player Actions Label Text", "Player actions:" },

                { "Id Label Format", "ID: {0}{1}" },
                { "Auth Level Label Format", "Auth level: {0}" },
                { "Connection Label Format", "Connection: {0}" },
                { "Status Label Format", "Status: {0} and {1}" },
                { "Flags Label Format", "Flags:{0}{1}" },
                { "Position Label Format", "Position: {0}" },
                { "Rotation Label Format", "Rotation: {0}" },
                { "Last Admin Cheat Label Format", "Last admin cheat: {0}" },
                { "Idle Time Label Format", "Idle time: {0} seconds" },
                { "Economics Balance Label Format", "Balance: {0} coins" },
                { "ServerRewards Points Label Format", "Reward points: {0}" },
                { "Health Label Format", "Health: {0}" },
                { "Calories Label Format", "Calories: {0}" },
                { "Hydration Label Format", "Hydration: {0}" },
                { "Temp Label Format", "Temperature: {0}" },
                { "Wetness Label Format", "Wetness: {0}" },
                { "Comfort Label Format", "Comfort: {0}" },
                { "Bleeding Label Format", "Bleeding: {0}" },
                { "Radiation Label Format", "Radiation: {0}" },
                { "Radiation Protection Label Format", "Protection: {0}" },

                { "Main Tab Text", "Main" },
                { "Online Player Tab Text", "Online Players" },
                { "Offline Player Tab Text", "Offline Players" },
                { "Banned Player Tab Text", "Banned Players" },

                { "Go Button Text", "Go" },

                { "Unban Button Text", "Unban" },
                { "Ban Button Text", "Ban" },
                { "Kick Button Text", "Kick" },

                { "Voice Unmute Button Text", "Unmute Voice" },
                { "Voice Mute Button Text", "Mute Voice" },

                { "Chat Unmute Button Text", "Unmute Chat" },
                { "Chat Mute Button Text", "Mute Chat" },
                { "Chat Mute Button Text 15", "Mute Chat 15 Min" },
                { "Chat Mute Button Text 30", "Mute Chat 30 Min" },
                { "Chat Mute Button Text 60", "Mute Chat 60 Min" },

                { "UnFreeze Button Text", "UnFreeze" },
                { "Freeze Button Text", "Freeze" },
                { "Freeze Not Installed Button Text", "Freeze Not Installed" },

                { "Clear Inventory Button Text", "Clear Inventory" },
                { "Reset Blueprints Button Text", "Reset Blueprints" },
                { "Reset Metabolism Button Text", "Reset Metabolism" },
                { "Recover Metabolism Button Text", "Recover Metabolism" },

                { "Teleport To Player Button Text", "Teleport To Player" },
                { "Teleport Player Button Text", "Teleport Player" },
                { "Spectate Player Button Text", "Spectate Player" },

                { "Perms Button Text", "Permissions" },
                { "Perms Not Installed Button Text", "Perms Not Installed" },

                { "Hurt 25 Button Text", "Hurt 25" },
                { "Hurt 50 Button Text", "Hurt 50" },
                { "Hurt 75 Button Text", "Hurt 75" },
                { "Hurt 100 Button Text", "Hurt 100" },
                { "Kill Button Text", "Kill" },

                { "Heal 25 Button Text", "Heal 25" },
                { "Heal 50 Button Text", "Heal 50" },
                { "Heal 75 Button Text", "Heal 75" },
                { "Heal 100 Button Text", "Heal 100" },
                { "Heal Wounds Button Text", "Heal Wounds" }
            }, this, "en");
            LogDebug("Default messages loaded");
        }

        protected override void SaveConfig() => Config.WriteObject(FConfigData);
        #endregion Hooks

        #region Command Callbacks
        [ChatCommand(CPadminCmd)]
        private void PlayerAdministrationUICallback(BasePlayer aPlayer, string aCommand, string[] aArgs)
        {
            LogDebug("PlayerAdministrationUICallback was called");

            if (!VerifyPermission(ref aPlayer, string.Empty, true))
                return;

            LogInfo($"{aPlayer.displayName}: Opened the menu");
            BuildUI(aPlayer, UiPage.Main);
        }

        [ConsoleCommand(CCloseUiCmd)]
        private void PlayerAdministrationCloseUICallback(ConsoleSystem.Arg aArg)
        {
            LogDebug("PlayerAdministrationCloseUICallback was called");
            BasePlayer player = aArg.Player();
            CuiHelper.DestroyUi(aArg.Player(), CMainPanelName);

            if (FMainPageBanIdInputText.ContainsKey(player.userID))
                FMainPageBanIdInputText.Remove(player.userID);

            if (FUserBtnPageSearchInputText.ContainsKey(player.userID))
                FUserBtnPageSearchInputText.Remove(player.userID);
        }

        [ConsoleCommand(CSwitchUiCmd)]
        private void PlayerAdministrationSwitchUICallback(ConsoleSystem.Arg aArg)
        {
            LogDebug("PlayerAdministrationSwitchUICallback was called");
            BasePlayer player = aArg.Player();

            if (!VerifyPermission(ref player, string.Empty, true) || !aArg.HasArgs())
                return;

            switch (aArg.Args[0].ToLower()) {
                case CCmdArgPlayersOnline: {
                    BuildUI(player, UiPage.PlayersOnline, (aArg.HasArgs(2) ? aArg.Args[1] : string.Empty));
                    break;
                }
                case CCmdArgPlayersOnlineSearch: {
                    BuildUI(player, UiPage.PlayersOnline, (aArg.HasArgs(2) ? aArg.Args[1] : string.Empty), true);
                    break;
                }
                case CCmdArgPlayersOffline: {
                    BuildUI(player, UiPage.PlayersOffline, (aArg.HasArgs(2) ? aArg.Args[1] : string.Empty));
                    break;
                }
                case CCmdArgPlayersOfflineSearch: {
                    BuildUI(player, UiPage.PlayersOffline, (aArg.HasArgs(2) ? aArg.Args[1] : string.Empty), true);
                    break;
                }
                case CCmdArgPlayersBanned: {
                    BuildUI(player, UiPage.PlayersBanned, (aArg.HasArgs(2) ? aArg.Args[1] : string.Empty));
                    break;
                }
                case CCmdArgPlayersBannedSearch: {
                    BuildUI(player, UiPage.PlayersBanned, (aArg.HasArgs(2) ? aArg.Args[1] : string.Empty), true);
                    break;
                }
                case CCmdArgPlayerPage: {
                    BuildUI(player, UiPage.PlayerPage, (aArg.HasArgs(2) ? aArg.Args[1] : string.Empty));
                    break;
                }
                case CCmdArgPlayerPageBanned: {
                    BuildUI(player, UiPage.PlayerPageBanned, (aArg.HasArgs(2) ? aArg.Args[1] : string.Empty));
                    break;
                }
                default: { // Main is the default for everything
                    BuildUI(player, UiPage.Main);
                    break;
                }
            }
        }

        [ConsoleCommand(CUnbanUserCmd)]
        private void PlayerAdministrationUnbanUserCallback(ConsoleSystem.Arg aArg)
        {
            LogDebug("PlayerAdministrationUnbanUserCallback was called");
            BasePlayer player = aArg.Player();
            ulong targetId;

            if (!VerifyPermission(ref player, CPermBan, true) || !GetTargetFromArg(ref aArg, out targetId))
                return;

            Player.Unban(targetId);
            LogInfo($"{player.displayName}: Unbanned user ID {targetId}");
            BuildUI(player, UiPage.Main);
        }

        [ConsoleCommand(CBanUserCmd)]
        private void PlayerAdministrationBanUserCallback(ConsoleSystem.Arg aArg)
        {
            LogDebug("PlayerAdministrationBanUserCallback was called");
            BasePlayer player = aArg.Player();
            ulong targetId;

            if (!VerifyPermission(ref player, CPermBan, true) || !GetTargetFromArg(ref aArg, out targetId))
                return;

            string banReasonMsg = GetMessage("Ban Reason Message Text", targetId.ToString());
            Player.Ban(targetId, banReasonMsg);
            ServerUsers.User targetPlayer = ServerUsers.Get(targetId);
            LogInfo($"{player.displayName}: Banned user ID {targetId}");
            SendDiscordKickBanMessage(player.displayName, player.UserIDString, targetPlayer.username, targetId.ToString(), banReasonMsg, true);
            BuildUI(player, UiPage.PlayerPage, targetId.ToString());
        }

        [ConsoleCommand(CMainPageBanByIdCmd)]
        private void PlayerAdministrationMainPageBanByIdCallback(ConsoleSystem.Arg aArg)
        {
            LogDebug("PlayerAdministrationMainPageBanByIdCallback was called");
            BasePlayer player = aArg.Player();
            ulong targetId;

            if (!VerifyPermission(ref player, CPermBan, true) || !FMainPageBanIdInputText.ContainsKey(player.userID) ||
                !ulong.TryParse(FMainPageBanIdInputText[player.userID], out targetId))
                return;

            string banReasonMsg = GetMessage("Ban Reason Message Text", targetId.ToString());
            Player.Ban(targetId, banReasonMsg);
            ServerUsers.User targetPlayer = ServerUsers.Get(targetId);
            LogInfo($"{player.displayName}: Banned user ID {targetId}");
            SendDiscordKickBanMessage(player.displayName, player.UserIDString, targetPlayer.username, targetId.ToString(), banReasonMsg, true);
            BuildUI(player, UiPage.Main);
        }

        [ConsoleCommand(CKickUserCmd)]
        private void PlayerAdministrationKickUserCallback(ConsoleSystem.Arg aArg)
        {
            LogDebug("PlayerAdministrationKickUserCallback was called");
            BasePlayer player = aArg.Player();
            ulong targetId;

            if (!VerifyPermission(ref player, CPermKick, true) || !GetTargetFromArg(ref aArg, out targetId))
                return;

            BasePlayer targetPlayer = BasePlayer.FindByID(targetId);
            string kickReasonMsg = GetMessage("Kick Reason Message Text", targetId.ToString());
            targetPlayer?.Kick(kickReasonMsg);
            LogInfo($"{player.displayName}: Kicked user ID {targetId}");
            SendDiscordKickBanMessage(player.displayName, player.UserIDString, targetPlayer.displayName, targetPlayer.UserIDString, kickReasonMsg, false);
            BuildUI(player, UiPage.PlayerPage, targetId.ToString());
        }

        [ConsoleCommand(CVoiceUnmuteUserCmd)]
        private void PlayerAdministrationVoiceUnmuteUserCallback(ConsoleSystem.Arg aArg)
        {
            LogDebug("PlayerAdministrationVoiceUnmuteUserCallback was called");
            BasePlayer player = aArg.Player();
            ulong targetId;

            if (!VerifyPermission(ref player, CPermVoiceMute, true) || !GetTargetFromArg(ref aArg, out targetId))
                return;

            (BasePlayer.FindByID(targetId) ?? BasePlayer.FindSleeping(targetId))?.SetPlayerFlag(BasePlayer.PlayerFlags.VoiceMuted, false);
            LogInfo($"{player.displayName}: Voice unmuted user ID {targetId}");
            BuildUI(player, UiPage.PlayerPage, targetId.ToString());
        }

        [ConsoleCommand(CVoiceMuteUserCmd)]
        private void PlayerAdministrationVoiceMuteUserCallback(ConsoleSystem.Arg aArg)
        {
            LogDebug("PlayerAdministrationVoiceMuteUserCallback was called");
            BasePlayer player = aArg.Player();
            ulong targetId;

            if (!VerifyPermission(ref player, CPermVoiceMute, true) || !GetTargetFromArg(ref aArg, out targetId))
                return;

            (BasePlayer.FindByID(targetId) ?? BasePlayer.FindSleeping(targetId))?.SetPlayerFlag(BasePlayer.PlayerFlags.VoiceMuted, true);
            LogInfo($"{player.displayName}: Voice muted user ID {targetId}");
            BuildUI(player, UiPage.PlayerPage, targetId.ToString());
        }

        [ConsoleCommand(CChatUnmuteUserCmd)]
        private void PlayerAdministrationChatUnmuteUserCallback(ConsoleSystem.Arg aArg)
        {
            LogDebug("PlayerAdministrationChatUnmuteUserCallback was called");
            BasePlayer player = aArg.Player();
            ulong targetId;

            if (!VerifyPermission(ref player, CPermChatMute, true) || !GetTargetFromArg(ref aArg, out targetId))
                return;

            BasePlayer target = BasePlayer.FindByID(targetId) ?? BasePlayer.FindSleeping(targetId);

            if (BetterChatMute != null && target != null)
                BetterChatMute.Call("API_Unmute", target.IPlayer, player.IPlayer);

            target?.SetPlayerFlag(BasePlayer.PlayerFlags.ChatMute, false);
            LogInfo($"{player.displayName}: Chat unmuted user ID {targetId}");
            BuildUI(player, UiPage.PlayerPage, targetId.ToString());
        }

        [ConsoleCommand(CChatMuteUserCmd)]
        private void PlayerAdministrationChatMuteUserCallback(ConsoleSystem.Arg aArg)
        {
            LogDebug("PlayerAdministrationChatMuteUserCallback was called");
            BasePlayer player = aArg.Player();
            ulong targetId;
            float time;

            if (!VerifyPermission(ref player, CPermChatMute, true) || !GetTargetAmountFromArg(ref aArg, out targetId, out time))
                return;

            BasePlayer target = BasePlayer.FindByID(targetId) ?? BasePlayer.FindSleeping(targetId);

            if (BetterChatMute != null && target != null) {
                if (time == 0f) {
                    BetterChatMute.Call("API_Mute", target.IPlayer, player.IPlayer);
                } else {
                    BetterChatMute.Call("API_TimeMute", target.IPlayer, player.IPlayer, TimeSpan.FromMinutes(time));
                }
            } else {
                target?.SetPlayerFlag(BasePlayer.PlayerFlags.ChatMute, true);
            }

            LogInfo($"{player.displayName}: Chat muted user ID {targetId}");
            BuildUI(player, UiPage.PlayerPage, targetId.ToString());
        }

        [ConsoleCommand(CUnreezeCmd)]
        private void PlayerAdministrationUnfreezeCallback(ConsoleSystem.Arg aArg)
        {
            LogDebug("PlayerAdministrationUnfreezeCallback was called");
            BasePlayer player = aArg.Player();
            ulong targetId;

            if (!VerifyPermission(ref player, CPermFreeze, true) || !GetTargetFromArg(ref aArg, out targetId))
                return;

            player.SendConsoleCommand($"{CFreezeUnfreezeCmd} {targetId}");
            LogInfo($"{player.displayName}: Chat unfroze user ID {targetId}");
            // Let code execute, then reload screen
            timer.Once(0.1f, () => BuildUI(player, UiPage.PlayerPage, targetId.ToString()));
        }

        [ConsoleCommand(CFreezeCmd)]
        private void PlayerAdministrationFreezeCallback(ConsoleSystem.Arg aArg)
        {
            LogDebug("PlayerAdministrationFreezeCallback was called");
            BasePlayer player = aArg.Player();
            ulong targetId;

            if (!VerifyPermission(ref player, CPermFreeze, true) || !GetTargetFromArg(ref aArg, out targetId))
                return;

            player.SendConsoleCommand($"{CFreezeFreezeCmd} {targetId}");
            LogInfo($"{player.displayName}: Chat froze user ID {targetId}");
            // Let code execute, then reload screen
            timer.Once(0.1f, () => BuildUI(player, UiPage.PlayerPage, targetId.ToString()));
        }

        [ConsoleCommand(CClearUserInventoryCmd)]
        private void PlayerAdministrationClearUserInventoryCallback(ConsoleSystem.Arg aArg)
        {
            LogDebug("PlayerAdministrationClearUserInventoryCallback was called");
            BasePlayer player = aArg.Player();
            ulong targetId;

            if (!VerifyPermission(ref player, CPermClearInventory, true) || !GetTargetFromArg(ref aArg, out targetId))
                return;

            (BasePlayer.FindByID(targetId) ?? BasePlayer.FindSleeping(targetId))?.inventory.Strip();
            LogInfo($"{player.displayName}: Cleared the inventory of user ID {targetId}");
            BuildUI(player, UiPage.PlayerPage, targetId.ToString());
        }

        [ConsoleCommand(CResetUserBPCmd)]
        private void PlayerAdministrationResetUserBlueprintsCallback(ConsoleSystem.Arg aArg)
        {
            LogDebug("PlayerAdministrationResetUserBlueprintsCallback was called");
            BasePlayer player = aArg.Player();
            ulong targetId;

            if (!VerifyPermission(ref player, CPermResetBP, true) || !GetTargetFromArg(ref aArg, out targetId))
                return;

            (BasePlayer.FindByID(targetId) ?? BasePlayer.FindSleeping(targetId))?.blueprints.Reset();
            LogInfo($"{player.displayName}: Reset the blueprints of user ID {targetId}");
            BuildUI(player, UiPage.PlayerPage, targetId.ToString());
        }

        [ConsoleCommand(CResetUserMetabolismCmd)]
        private void PlayerAdministrationResetUserMetabolismCallback(ConsoleSystem.Arg aArg)
        {
            LogDebug("PlayerAdministrationResetUserMetabolismCallback was called");
            BasePlayer player = aArg.Player();
            ulong targetId;

            if (!VerifyPermission(ref player, CPermResetMetabolism, true) || !GetTargetFromArg(ref aArg, out targetId))
                return;

            (BasePlayer.FindByID(targetId) ?? BasePlayer.FindSleeping(targetId))?.metabolism.Reset();
            LogInfo($"{player.displayName}: Reset the metabolism of user ID {targetId}");
            BuildUI(player, UiPage.PlayerPage, targetId.ToString());
        }

        [ConsoleCommand(CRecoverUserMetabolismCmd)]
        private void PlayerAdministrationRecoverUserMetabolismCallback(ConsoleSystem.Arg aArg)
        {
            LogDebug("PlayerAdministrationRecoverUserMetabolismCallback was called");
            BasePlayer player = aArg.Player();
            ulong targetId;

            if (!VerifyPermission(ref player, CPermRecoverMetabolism, true) || !GetTargetFromArg(ref aArg, out targetId))
                return;

            PlayerMetabolism playerState = (BasePlayer.FindByID(targetId) ?? BasePlayer.FindSleeping(targetId))?.metabolism;
            playerState.bleeding.value = playerState.bleeding.min;
            playerState.calories.value = playerState.calories.max;
            playerState.comfort.value = 0;
            playerState.hydration.value = playerState.hydration.max;
            playerState.oxygen.value = playerState.oxygen.max;
            playerState.poison.value = playerState.poison.min;
            playerState.radiation_level.value = playerState.radiation_level.min;
            playerState.radiation_poison.value = playerState.radiation_poison.min;
            playerState.temperature.value = (PlayerMetabolism.HotThreshold + PlayerMetabolism.ColdThreshold) / 2;
            playerState.wetness.value = playerState.wetness.min;

            LogInfo($"{player.displayName}: Recovered the metabolism of user ID {targetId}");
            BuildUI(player, UiPage.PlayerPage, targetId.ToString());
        }

        [ConsoleCommand(CTeleportToUserCmd)]
        private void PlayerAdministrationTeleportToUserCallback(ConsoleSystem.Arg aArg)
        {
            LogDebug("PlayerAdministrationTeleportToUserCallback was called");
            BasePlayer player = aArg.Player();
            ulong targetId;

            if (!VerifyPermission(ref player, CPermTeleport, true) || !GetTargetFromArg(ref aArg, out targetId))
                return;

            player.Teleport(BasePlayer.FindByID(targetId) ?? BasePlayer.FindSleeping(targetId));
            LogInfo($"{player.displayName}: Teleported to user ID {targetId}");
            BuildUI(player, UiPage.PlayerPage, targetId.ToString());
        }

        [ConsoleCommand(CTeleportUserCmd)]
        private void PlayerAdministrationTeleportUserCallback(ConsoleSystem.Arg aArg)
        {
            LogDebug("PlayerAdministrationTeleportUserCallback was called");
            BasePlayer player = aArg.Player();
            ulong targetId;

            if (!VerifyPermission(ref player, CPermTeleport, true) || !GetTargetFromArg(ref aArg, out targetId))
                return;

            BasePlayer targetPlayer = BasePlayer.FindByID(targetId) ?? BasePlayer.FindSleeping(targetId);
            targetPlayer.Teleport(player);
            LogInfo($"{targetPlayer.displayName}: Teleported to admin {player.displayName}");
            BuildUI(player, UiPage.PlayerPage, targetId.ToString());
        }

        [ConsoleCommand(CSpectateUserCmd)]
        private void PlayerAdministrationSpectateUserCallback(ConsoleSystem.Arg aArg)
        {
            LogDebug("PlayerAdministrationSpectateUserCallback was called");
            BasePlayer player = aArg.Player();
            ulong targetId;

            if (!VerifyPermission(ref player, CPermSpectate, true) || !GetTargetFromArg(ref aArg, out targetId))
                return;

            if (!player.IsDead())
                player.DieInstantly();

            player.StartSpectating();
            player.UpdateSpectateTarget(targetId.ToString());
            LogInfo($"{player.displayName}: Started spectating user ID {targetId}");
            BuildUI(player, UiPage.PlayerPage, targetId.ToString());
        }

        [ConsoleCommand(CPermsCmd)]
        private void PlayerAdministrationRunPermsCallback(ConsoleSystem.Arg aArg)
        {
            LogDebug("PlayerAdministrationRunPermsCallback was called");
            BasePlayer player = aArg.Player();
            ulong targetId;

            if (!VerifyPermission(ref player, CPermPerms, true) || !GetTargetFromArg(ref aArg, out targetId))
                return;

            player.SendConsoleCommand($"chat.say \"/{CPermsPermsCmd} {targetId}\"");
            LogInfo($"{player.displayName}: Opened the permissions manager for user ID {targetId}");
        }

        [ConsoleCommand(CHurtUserCmd)]
        private void PlayerAdministrationHurtUserCallback(ConsoleSystem.Arg aArg)
        {
            LogDebug("PlayerAdministrationHurtUserCallback was called");
            BasePlayer player = aArg.Player();
            ulong targetId;
            float amount;

            if (!VerifyPermission(ref player, CPermHurt, true) || !GetTargetAmountFromArg(ref aArg, out targetId, out amount))
                return;

            (BasePlayer.FindByID(targetId) ?? BasePlayer.FindSleeping(targetId))?.Hurt(amount);
            LogInfo($"{player.displayName}: Hurt user ID {targetId} for {amount} points");
            BuildUI(player, UiPage.PlayerPage, targetId.ToString());
        }

        [ConsoleCommand(CKillUserCmd)]
        private void PlayerAdministrationKillUserCallback(ConsoleSystem.Arg aArg)
        {
            LogDebug("PlayerAdministrationKillUserCallback was called");
            BasePlayer player = aArg.Player();
            ulong targetId;

            if (!VerifyPermission(ref player, CPermKill, true) || !GetTargetFromArg(ref aArg, out targetId))
                return;

            (BasePlayer.FindByID(targetId) ?? BasePlayer.FindSleeping(targetId))?.Die();
            LogInfo($"{player.displayName}: Killed user ID {targetId}");
            BuildUI(player, UiPage.PlayerPage, targetId.ToString());
        }

        [ConsoleCommand(CHealUserCmd)]
        private void PlayerAdministrationHealUserCallback(ConsoleSystem.Arg aArg)
        {
            LogDebug("PlayerAdministrationHealUserCallback was called");
            BasePlayer player = aArg.Player();
            ulong targetId;
            float amount;

            if (!VerifyPermission(ref player, CPermHeal, true) || !GetTargetAmountFromArg(ref aArg, out targetId, out amount))
                return;

            BasePlayer targetPlayer = BasePlayer.FindByID(targetId) ?? BasePlayer.FindSleeping(targetId);

            if (targetPlayer.IsWounded())
                targetPlayer.StopWounded();

            targetPlayer.Heal(amount);
            LogInfo($"{player.displayName}: Healed user ID {targetId} for {amount} points");
            BuildUI(player, UiPage.PlayerPage, targetId.ToString());
        }
        #endregion Command Callbacks

        #region Text Update Callbacks
        [ConsoleCommand(CMainPageBanIdInputTextCmd)]
        private void PlayerAdministrationMainPageBanIdInputTextCallback(ConsoleSystem.Arg aArg)
        {
            BasePlayer player = aArg.Player();

            if (!VerifyPermission(ref player, CPermUiShow) || !aArg.HasArgs()) {
                if (FMainPageBanIdInputText.ContainsKey(player.userID))
                    FMainPageBanIdInputText.Remove(player.userID);

                return;
            }

            if (FMainPageBanIdInputText.ContainsKey(player.userID)) {
                FMainPageBanIdInputText[player.userID] = aArg.Args[0];
            } else {
                FMainPageBanIdInputText.Add(player.userID, aArg.Args[0]);
            }
        }

        [ConsoleCommand(CUserBtnPageSearchInputTextCmd)]
        private void PlayerAdministrationUserBtnPageSearchInputTextCallback(ConsoleSystem.Arg aArg)
        {
            BasePlayer player = aArg.Player();

            if (!VerifyPermission(ref player, CPermUiShow) || !aArg.HasArgs()) {
                if (FUserBtnPageSearchInputText.ContainsKey(player.userID))
                    FUserBtnPageSearchInputText.Remove(player.userID);

                return;
            }

            if (FUserBtnPageSearchInputText.ContainsKey(player.userID)) {
                FUserBtnPageSearchInputText[player.userID] = aArg.Args[0];
            } else {
                FUserBtnPageSearchInputText.Add(player.userID, aArg.Args[0]);
            }
        }
        #endregion Text Update Callbacks
    }
}