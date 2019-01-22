using System.Collections.Generic;
using System;
using System.Linq;
using UnityEngine;
using Oxide.Core.Plugins;
using Oxide.Core.Configuration;
using Oxide.Game.Rust.Cui;
using Oxide.Core;
using System.Reflection;

namespace Oxide.Plugins
{
    [Info("AbsolutMail", "Absolut", "1.0.7", ResourceId = 2255)]

    class AbsolutMail : RustPlugin
    {
        [PluginReference]
        Plugin ImageLibrary;

        SavedData data;
        private DynamicConfigFile Data;
        static FieldInfo buildingPrivlidges = typeof(BasePlayer).GetField("buildingPrivilege", BindingFlags.Instance | BindingFlags.NonPublic);

        string TitleColor = "<color=#6F192A>";
        string MsgColor = "<color=#A9A9A9>";
        private MethodInfo updateTrigger = typeof(BuildingPrivlidge).GetMethod("UpdateAllPlayers", BindingFlags.Instance | BindingFlags.NonPublic);
        private Dictionary<string, Timer> timers = new Dictionary<string, Timer>();
        private List<ulong> MailBoxCooldown = new List<ulong>();
        private Dictionary<ulong, Info> UIinfo = new Dictionary<ulong, Info>();
        class Info
        {
            public int page;
            public Mail CurrentMail;
            public bool Composing;
        }

        private Vector3 eyesAdjust;
        private FieldInfo serverinput;

        #region Oxide Hooks
        void Loaded()
        {
            Data = Interface.Oxide.DataFileSystem.GetFile("AbsolutMail_Data");
            lang.RegisterMessages(messages, this);
        }

        void Unload()
        {
            SaveData();
            foreach (var player in BasePlayer.activePlayerList)
                DestroyUI(player);
        }

        void OnServerInitialized()
        {
            try
            {
                ImageLibrary.Call("isLoaded", null);
            }
            catch (Exception)
            {
                PrintWarning($"ImageLibrary is missing. Unloading {Name} as it will not work without ImageLibrary.");
                Interface.Oxide.UnloadPlugin(Name);
                return;
            }
            eyesAdjust = new Vector3(0f, 1.5f, 0f);
            serverinput = typeof(BasePlayer).GetField("serverInput", (BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic));
            LoadVariables();
            LoadData();
            AddImage("https://cdn4.iconfinder.com/data/icons/SUPERVISTA/mail_icons/png/400/mailbox.png", "mailbox", (ulong)ResourceId);
            AddImage("http://icongal.com/gallery/image/153090/new_unread_mail_status.png", "NewMail", (ulong)ResourceId);
            permission.RegisterPermission("AbsolutMail.admin", this);
            permission.RegisterPermission("AbsolutMail.VIP1", this);
            permission.RegisterPermission("AbsolutMail.VIP2", this);
            permission.RegisterPermission("AbsolutMail.VIP3", this);
            timers.Add("info", timer.Once(900, () => InfoLoop()));
            timers.Add("save", timer.Once(600, () => SaveLoop()));
            foreach (var player in BasePlayer.activePlayerList)
                OnPlayerInit(player);
        }

        private void OnPlayerInit(BasePlayer player)
        {
            if (player != null)
            {
                BindKeys(player);
                if (!UIinfo.ContainsKey(player.userID))
                    UIinfo.Add(player.userID, new Info { page = 0, Composing = false, CurrentMail = new Mail() });
                if (!data.SavedPlayers.ContainsKey(player.userID))
                    data.SavedPlayers.Add(player.userID, new Player { displayname = player.displayName, theme = null });
                else
                {
                    data.SavedPlayers[player.userID].displayname = player.displayName;
                    if (data.SavedPlayers[player.userID].theme != null) AddImage(data.SavedPlayers[player.userID].theme, player.displayName, (ulong)ResourceId);
                }   
                timer.Once(10, () =>
                {
                    MailBoxIcon(player);
                });
            }
        }

        private void OnPlayerRespawned(BasePlayer player)
        {
            DestroyUI(player);
            MailBoxIcon(player);
        }

        private void OnPlayerDisconnected(BasePlayer player)
        {
            DestroyUI(player);
            SaveData();
        }

        void DestroyUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, PanelIcon);
            CuiHelper.DestroyUi(player, PanelMail);
            CuiHelper.DestroyUi(player, PanelMessage);
        }

        object OnServerCommand(ConsoleSystem.Arg arg)
        {
            //Puts("TRYING COMMAND");
            var player = arg.Connection?.player as BasePlayer;
            if (player == null)
                return null;
            if (UIinfo[player.userID].Composing && arg.cmd?.FullName == "chat.say")
            {
                //Puts("TRUE");
                if (arg.Args.Contains("quit"))
                {
                    GetSendMSG(player, "ComposingEnded");
                    UIinfo[player.userID].Composing = false;
                    return false;
                }
                if (UIinfo[player.userID].CurrentMail.subject == null)
                {
                    UIinfo[player.userID].CurrentMail.subject = string.Join(" ", arg.Args);
                    CuiHelper.DestroyUi(player, PanelMail);
                    var element = UI.CreateElementContainer(PanelMail, "0 0 0 0", ".35 .3", ".65 .5");
                    UI.CreateTextOutline(ref element, PanelMail, UIColors["black"], UIColors["white"], GetLang("ProvideMessage", player), 18, $"0 0", "1 1", TextAnchor.MiddleCenter);
                    CuiHelper.AddUi(player, element);
                }
                else
                {
                    UIinfo[player.userID].CurrentMail.message = string.Join(" ", arg.Args);
                    if (configData.AllowAttachments)
                        if (player.inventory.containerMain.itemList.Count > 0)
                        {
                            AskToAttachItem(player);
                            return false;
                        }
                    UIinfo[player.userID].CurrentMail.attachment = null;
                    SendMail(player, UIinfo[player.userID].CurrentMail);
                }
                return false;
            }
            return null;
        }

        void AskToAttachItem(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, PanelMail);
            var element = UI.CreateElementContainer(PanelMail, "0 0 0 0", ".35 .3", ".65 .5", true);
            UI.CreateTextOutline(ref element, PanelMail, UIColors["black"], UIColors["white"], GetLang("AttachAnItem", player), 12, $"0 0", "1 1", TextAnchor.UpperCenter);
            UI.CreateButton(ref element, PanelMail, UIColors["green"], GetLang("Yes", player), 12, $".3 .1", ".45 .4", $"UI_AttachItem yes");
            UI.CreateButton(ref element, PanelMail, UIColors["red"], GetLang("No", player), 12, $".55 .1", ".7 .4", $"UI_AttachItem no");
            CuiHelper.AddUi(player, element);
        }


        #endregion

        #region Functions
        private string GetLang(string msg, BasePlayer player = null)
        {
            if (messages.ContainsKey(msg))
                return lang.GetMessage(msg, this);
            else return msg;
        }

        private void GetSendMSG(BasePlayer player, string message, string arg1 = "", string arg2 = "", string arg3 = "")
        {
            string msg = string.Format(lang.GetMessage(message, this, player.UserIDString), arg1, arg2, arg3);
            SendReply(player, TitleColor + lang.GetMessage("title", this, player.UserIDString) + "</color>" + MsgColor + msg + "</color>");
        }

        private string GetMSG(string message, BasePlayer player = null, string arg1 = "", string arg2 = "", string arg3 = "")
        {
            string p = null;
            if (player != null)
                p = player.UserIDString;
            string msg = string.Format(lang.GetMessage(message, this, p), arg1, arg2, arg3);
            return msg;
        }

        bool isAuth(BasePlayer player)
        {
            if (player.net.connection != null)
                if (player.net.connection.authLevel < 2)
                    return false;
            return true;
        }
        private string TryForImage(string shortname, ulong skin = 99)
        {
            if (shortname.Contains("http")) return shortname;
            if (skin == 99) skin = (ulong)ResourceId;
            return GetImage(shortname, skin, true);
        }

        public string GetImage(string shortname, ulong skin = 0, bool returnUrl = false) => (string)ImageLibrary.Call("GetImage", shortname.ToLower(), skin, returnUrl);
        public bool HasImage(string shortname, ulong skin = 0) => (bool)ImageLibrary.Call("HasImage", shortname.ToLower(), skin);
        public bool AddImage(string url, string shortname, ulong skin = 0) => (bool)ImageLibrary?.Call("AddImage", url, shortname.ToLower(), skin);
        public List<ulong> GetImageList(string shortname) => (List<ulong>)ImageLibrary.Call("GetImageList", shortname.ToLower());
        public bool isReady() => (bool)ImageLibrary?.Call("IsReady");
        #endregion

        #region MailBox Functions

        void BindKeys(BasePlayer player, bool off = false)
        {
            if (!off)
            {
                player.Command($"bind {configData.MailKeyBinding} \"cmdMail\"");
            }
            else
            {
             if(configData.MailKeyBinding == "e")
                    player.Command("bind e \"+use\"");
             else player.Command($"bind {configData.MailKeyBinding} \"\"");
            }
        }


        static bool AllowedToBuild(BasePlayer player)
        {
            //if (player == null) return false;
            List<BuildingPrivlidge> playerpriv = buildingPrivlidges.GetValue(player) as List<BuildingPrivlidge>;
            if (playerpriv == null || playerpriv.Count == 0)
            {
                return false;
            }
            foreach (BuildingPrivlidge priv in playerpriv.ToArray())
            {
                List<ProtoBuf.PlayerNameID> authorized = priv.authorizedPlayers;
                bool foundplayer = false;
                foreach (ProtoBuf.PlayerNameID pni in authorized.ToArray())
                    if (pni.userid == player.userID)
                        foundplayer = true;
                if (!foundplayer)
                    return false;
            }
            return true;
        }

        void InitializeMailbox(BasePlayer player)
        {
            if (MailBoxCooldown.Contains(player.userID)) { GetSendMSG(player, "MailboxCooldown", configData.MailBoxCooldown.ToString()); return; }
            if (!AllowedToBuild(player)) { GetSendMSG(player, "NotInTCRadius"); return; }
            uint ID = 0;
            if (data.Mailboxes.ContainsValue(player.userID))
            {
                foreach (var entry in data.Mailboxes.Where(k => k.Value == player.userID))
                    ID = entry.Key;
                foreach (var ent in BaseNetworkable.serverEntities.Where(p => p.net.ID == ID))
                {
                    ent.Kill();
                    break;
                }
                data.Mailboxes.Remove(ID);
            }
            var pos = player.transform.localPosition;
            BaseEntity entity = GameManager.server.CreateEntity("assets/prefabs/deployable/signs/sign.post.single.prefab", new Vector3 { x = pos.x, y = pos.y, z = pos.z }, Quaternion.Euler(0, player.transform.localRotation.y, 0), true);
            Signage sign = entity as Signage;
            if (sign != null)
            {
                sign.textureID = Convert.ToUInt32(TryForImage("mailbox"));
                //Puts(sign.textureID.ToString());
                sign.Spawn();
            }
            data.Mailboxes.Add(entity.net.ID, player.userID);
            if (configData.MailBoxCooldown == 0) return;
            MailBoxCooldown.Add(player.userID);
            if (timers.ContainsKey(player.UserIDString))
            {
                timers[player.UserIDString].Destroy();
                timers.Remove(player.UserIDString);
            }
            timers.Add(player.UserIDString, timer.Once(configData.MailBoxCooldown * 60, () => MailBoxCooldown.Remove(player.userID)));
        }

        private void SendMail(BasePlayer player, Mail mail)
        {
            ulong recipient = UIinfo[player.userID].CurrentMail.recipient;
            if (!data.mail.ContainsKey(recipient))
                data.mail.Add(recipient, new List<Mail>());
            data.mail[recipient].Add(UIinfo[player.userID].CurrentMail);
            UIinfo[player.userID].CurrentMail = null;
            UIinfo[player.userID].Composing = false;
            GetSendMSG(player, "SuccessfulMail");
            MailBoxScreen(player);
            SaveData();
            try
            {
                if (BasePlayer.activePlayerList.Contains(BasePlayer.FindByID(recipient)))
                    MailBoxIcon(BasePlayer.FindByID(recipient));
            }
            catch { }
        }

        private void GetAttachmentItem(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, PanelMail);
            if (!UIinfo.ContainsKey(player.userID) || !data.mail.ContainsKey(player.userID)) return;
            var element = UI.CreateElementContainer(PanelMail, UIColors["dark"], "0.3 0.3", "0.7 0.9", true);
            UI.CreatePanel(ref element, PanelMail, UIColors["light"], "0.01 0.02", "0.99 0.98");
            UI.CreateTextOutline(ref element, PanelMail, UIColors["black"], UIColors["white"], GetLang("SelectAttachment", player), 20, "0 .95", "1 1");
            var i = 0;
            foreach (var entry in player.inventory.containerMain.itemList)
            {
                var pos = CalcButtonPos(i);
                UI.LoadImage(ref element, PanelMail, TryForImage(entry.info.shortname, entry.skin), $"{ pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}");
                UI.CreateButton(ref element, PanelMail, "0 0 0 0", "", 10, $"{ pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"UI_SelectItem {entry.uid}");
                i++;
            }
            UI.CreateButton(ref element, PanelMail, UIColors["red"], GetLang("No", player), 12, "0.13 0.03", "0.43 0.08", $"UI_AttachItem no");
            CuiHelper.AddUi(player, element);
        }
        object FindEntityFromRay(BasePlayer player)
        {
            var input = serverinput.GetValue(player) as InputState;
            Ray ray = new Ray(player.eyes.position, Quaternion.Euler(input.current.aimAngles) * Vector3.forward);
            RaycastHit hit;
            if (!Physics.Raycast(ray, out hit, 20))
                return null;
            var hitEnt = hit.collider.GetComponentInParent<Signage>();
            if (hitEnt != null)
                if (data.Mailboxes.ContainsKey(hitEnt.net.ID))
                    foreach (var entry in data.Mailboxes.Where(k => k.Key == hitEnt.net.ID))
                        return entry.Value;
            return null;
        }

        #endregion

        #region Timers

        private void SaveLoop()
        {
            if (timers.ContainsKey("save"))
            {
                timers["save"].Destroy();
                timers.Remove("save");
            }
            SaveData();
            timers.Add("save", timer.Once(600, () => SaveLoop()));
        }

        private void InfoLoop()
        {
            if (timers.ContainsKey("info"))
            {
                timers["info"].Destroy();
                timers.Remove("info");
            }
            if (configData.InfoInterval == 0) return;
            foreach (BasePlayer p in BasePlayer.activePlayerList)
            {
                var extra = "";
                if (configData.PhysicalMailBox) extra = GetMSG("PhysicalInfo",p, configData.MailKeyBinding);
                else extra = GetLang("VirtualInfo", p);
                if (configData.UseThemes) extra += GetLang("ThemeInfo");
                GetSendMSG(p, "MailInfo", extra);
            }
            timers.Add("info", timer.Once(configData.InfoInterval * 60, () => InfoLoop()));
        }

        #endregion

        #region UI Creation
        private string PanelIcon = "PanelIcon";
        private string PanelMail = "PanelMail";
        private string PanelMessage = "PanelMessage";

        public class UI
        {
            static public CuiElementContainer CreateElementContainer(string panelName, string color, string aMin, string aMax, bool cursor = false)
            {
                var NewElement = new CuiElementContainer()
            {
                {
                    new CuiPanel
                    {
                        Image = {Color = color},
                        RectTransform = {AnchorMin = aMin, AnchorMax = aMax},
                        CursorEnabled = cursor
                    },
                    new CuiElement().Parent,
                    panelName
                }
            };
                return NewElement;
            }
            static public CuiElementContainer CreateOverlayContainer(string panelName, string color, string aMin, string aMax, bool cursor = false)
            {
                var NewElement = new CuiElementContainer()
            {
                {
                    new CuiPanel
                    {
                        Image = {Color = color},
                        RectTransform = {AnchorMin = aMin, AnchorMax = aMax},
                        CursorEnabled = cursor
                    },
                    new CuiElement().Parent = "Overlay",
                    panelName
                }
            };
                return NewElement;
            }

            static public CuiElementContainer CreateHudContainer(string panelName, string color, string aMin, string aMax, bool cursor = false)
            {
                var NewElement = new CuiElementContainer()
            {
                {
                    new CuiPanel
                    {
                        Image = {Color = color},
                        RectTransform = {AnchorMin = aMin, AnchorMax = aMax},
                        CursorEnabled = cursor
                    },
                    new CuiElement().Parent = "HUD",
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
                panel);
            }
            static public void CreateLabel(ref CuiElementContainer container, string panel, string color, string text, int size, string aMin, string aMax, TextAnchor align = TextAnchor.MiddleCenter)
            {
                container.Add(new CuiLabel
                {
                    Text = { Color = color, FontSize = size, Align = align, FadeIn = 1.0f, Text = text },
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax }
                },
                panel);
            }

            static public void CreateButton(ref CuiElementContainer container, string panel, string color, string text, int size, string aMin, string aMax, string command, TextAnchor align = TextAnchor.MiddleCenter)
            {
                container.Add(new CuiButton
                {
                    Button = { Color = color, Command = command, FadeIn = 1.0f },
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax },
                    Text = { Text = text, FontSize = size, Align = align }
                },
                panel);
            }

            static public void LoadImage(ref CuiElementContainer container, string panel, string img, string aMin, string aMax)
            {
                if (img.StartsWith("http") || img.StartsWith("www"))
                {
                    container.Add(new CuiElement
                    {
                        Parent = panel,
                        Components =
                    {
                        new CuiRawImageComponent {Url = img, Sprite = "assets/content/textures/generic/fulltransparent.tga" },
                        new CuiRectTransformComponent {AnchorMin = aMin, AnchorMax = aMax }
                    }
                    });
                }
                else
                    container.Add(new CuiElement
                    {
                        Parent = panel,
                        Components =
                    {
                        new CuiRawImageComponent {Png = img, Sprite = "assets/content/textures/generic/fulltransparent.tga" },
                        new CuiRectTransformComponent {AnchorMin = aMin, AnchorMax = aMax }
                    }
                    });
            }

            static public void CreateTextOverlay(ref CuiElementContainer container, string panel, string text, string color, int size, string aMin, string aMax, TextAnchor align = TextAnchor.MiddleCenter, float fadein = 1.0f)
            {
                container.Add(new CuiLabel
                {
                    Text = { Color = color, FontSize = size, Align = align, FadeIn = fadein, Text = text },
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax }
                },
                panel);

            }
            static public void CreateTextOutline(ref CuiElementContainer element, string panel, string colorText, string colorOutline, string text, int size, string aMin, string aMax, TextAnchor align = TextAnchor.MiddleCenter)
            {
                element.Add(new CuiElement
                {
                    Parent = panel,
                    Components =
                    {
                        new CuiTextComponent{Color = colorText, FontSize = size, Align = align, Text = text },
                        new CuiOutlineComponent {Distance = "1 1", Color = colorOutline},
                        new CuiRectTransformComponent {AnchorMax = aMax, AnchorMin = aMin }
                    }
                });
            }
        }

        private Dictionary<string, string> UIColors = new Dictionary<string, string>
        {
            {"black", "0 0 0 1.0" },
            {"dark", "0.1 0.1 0.1 0.98" },
            {"header", "1 1 1 0.3" },
            {"light", ".564 .564 .564 1.0" },
            {"grey1", "0.6 0.6 0.6 1.0" },
            {"brown", "0.3 0.16 0.0 1.0" },
            {"yellow", "0.9 0.9 0.0 1.0" },
            {"orange", "1.0 0.65 0.0 1.0" },
            {"limegreen", "0.42 1.0 0 1.0" },
            {"blue", "0.2 0.6 1.0 1.0" },
            {"red", "1.0 0.1 0.1 1.0" },
            {"white", "1 1 1 1" },
            {"green", "0.28 0.82 0.28 1.0" },
            {"grey", "0.85 0.85 0.85 1.0" },
            {"spectator",  "0.9 0.9 0.0 1.0" },
            {"lightblue", "0.6 0.86 1.0 1.0" },
            {"buttonbg", "0.2 0.2 0.2 0.7" },
            {"buttongreen", "0.133 0.965 0.133 0.9" },
            {"buttonred", "0.964 0.133 0.133 0.9" },
            {"buttongrey", "0.8 0.8 0.8 0.9" },
            {"CSorange", "1.0 0.64 0.10 1.0" }
        };

        private Dictionary<string, string> ChatColor = new Dictionary<string, string>
        {
            {"blue", "<color=#3366ff>" },
            {"black", "<color=#000000>" },
            {"red", "<color=#e60000>" },
            {"green", "<color=#29a329>" },
            {"spectator", "<color=#ffff00>"}
        };

        #endregion

        #region UI Panels
        void MailBoxIcon(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, PanelIcon);
            if (!configData.UseMailBoxIcon) return;
            if (!data.mail.ContainsKey(player.userID)) return;
            int unread = data.mail[player.userID].Where(k => k.read == false).Count();
            if (unread == 0) return;
            var element = UI.CreateElementContainer(PanelIcon, "0 0 0 0"/*UIColors["dark"]*/, "0.78 0.95", "0.84 1");
            UI.LoadImage(ref element, PanelIcon, TryForImage("NewMail"), "0 0", "1 1");
            if (!configData.PhysicalMailBox)
                UI.CreateButton(ref element, PanelMail, "0 0 0 0","", 12, "0 0", "1 1", "UI_OpenMail");
            CuiHelper.AddUi(player, element);
        }

        void MailBoxScreen(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, PanelMail);
            if (!UIinfo.ContainsKey(player.userID))
                UIinfo.Add(player.userID, new Info { page = 0, Composing = false });
            if (!data.mail.ContainsKey(player.userID))
                data.mail.Add(player.userID, new List<Mail>());
            var element = UI.CreateElementContainer(PanelMail, UIColors["dark"], "0.2 0.2", "0.355 0.8", true);
            UI.CreatePanel(ref element, PanelMail, UIColors["light"], "0.02 0.01", "0.98 0.99");
            if (configData.UseThemes && data.SavedPlayers[player.userID].theme != null)
                if (GetImage(player.displayName, (ulong)ResourceId) != GetImage("nothingever"))
                    UI.LoadImage(ref element, PanelMail, TryForImage(player.displayName), "0.02 0.01", "0.98 0.99");
            UI.CreateTextOutline(ref element, PanelMail, UIColors["black"], UIColors["white"], GetLang("MailBox", player), 16, "0 .95", "1 1");
            UI.CreateButton(ref element, PanelMail, UIColors["blue"], GetLang("COMPOSE", player), 10, "0.03 .92", ".97 .95", "UI_SelectRecipient");
            var page = UIinfo[player.userID].page;
            int entriesallowed = 20;
            int remainingentries = data.mail[player.userID].Count - (page * entriesallowed);
            {
                if (remainingentries > entriesallowed)
                {
                    UI.CreateButton(ref element, PanelMail, UIColors["blue"], "Next", 10, "0.57 0.07", "0.87 0.1", $"UI_ChangeMailPage {page + 1}");
                }
                if (page > 0)
                {
                    UI.CreateButton(ref element, PanelMail, UIColors["buttonred"], "Back", 10, "0.03 0.07", "0.43 0.1", $"UI_ChangeMailPage {page - 1}");
                }
            }
            var i = 0;
            var n = 0;
            int shownentries = page * entriesallowed;
            foreach (var entry in data.mail[player.userID])
            {
                i++;
                if (i < shownentries + 1) continue;
                else if (i <= shownentries + entriesallowed)
                {
                    var pos = CalcMailLocation(n);
                    var color = ChatColor["blue"];
                    var msg = entry.subject;
                    if (entry.subject.Length > 30)
                        msg = entry.subject.Substring(0, 30);
                    if (entry.read)
                        color = ChatColor["red"];
                    UI.CreateButton(ref element, PanelMail, UIColors["buttongrey"] , color+msg+"</color>", 8, $"{ pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"UI_OpenMessage {i-1}");
                    n++;
                    //SHOW IF HAS ATTACHMENT
                    //DELETE BUTTON?
                }
            }
            UI.CreateButton(ref element, PanelMail, UIColors["red"], GetLang("Close", player), 10, "0.03 0.02", "0.97 0.06", $"UI_CloseUI");
            CuiHelper.AddUi(player, element);
        }

        void MessageScreen(BasePlayer player, Mail mail)
        {
            MailBoxScreen(player);
            CuiHelper.DestroyUi(player, PanelMessage);
            if (mail == null || !data.mail[player.userID].Contains(mail)) return;
            if (!UIinfo.ContainsKey(player.userID))
                UIinfo.Add(player.userID, new Info { page = 0, CurrentMail = mail, Composing = false });
            else
                UIinfo[player.userID].CurrentMail = mail;
            MailBoxIcon(player);
            var element = UI.CreateElementContainer(PanelMessage, UIColors["dark"], "0.36 0.2", "0.65 0.8", true);
            //UI.CreatePanel(ref element, PanelMessage, UIColors["light"], "0.01 0.02", "0.99 0.98");
            if (configData.UseThemes && data.SavedPlayers[player.userID].theme != null)
                if (GetImage(player.displayName, (ulong)ResourceId) != GetImage("nothingever"))
                    UI.LoadImage(ref element, PanelMessage, TryForImage(player.displayName), "0.01 0.01", "0.99 0.99");
            UI.CreatePanel(ref element, PanelMessage, UIColors["light"], "0.05 .93", ".95 .98");
            if (mail.subject != null)
                UI.CreateTextOutline(ref element, PanelMessage, UIColors["black"], UIColors["white"], mail.subject, 16, "0.05 .93", ".95 .98");
            UI.CreatePanel(ref element, PanelMessage, UIColors["light"], "0.05 .3", ".95 .9");
            if (mail.message != null)
                UI.CreateTextOutline(ref element, PanelMessage, UIColors["black"], UIColors["white"], mail.message, 12, "0.06 .3", ".94 .9", TextAnchor.UpperLeft);
            UI.CreatePanel(ref element, PanelMessage, UIColors["light"], "0.2 0.05", "0.7 0.1");
            if (mail.sender != null)
                UI.CreateTextOutline(ref element, PanelMessage, UIColors["black"], UIColors["white"], GetMSG("Sender", player, mail.sender), 10, "0.2 0.05", "0.7 0.1");
            UI.CreateTextOutline(ref element, PanelMessage, UIColors["black"], UIColors["white"], GetLang("Attachment", player), 12, "0.75 0.2", "0.95 0.25", TextAnchor.UpperCenter);
            UI.CreatePanel(ref element, PanelMessage, UIColors["light"], "0.75 0.05", "0.95 0.2");
            if (mail.attachment != null)
            {
                UI.LoadImage(ref element, PanelMessage, TryForImage(mail.attachment.shortname, mail.attachment.skin), "0.76 0.06", "0.94 0.19");
                UI.CreateButton(ref element, PanelMessage, "0 0 0 0", "", 10, "0.76 0.06", "0.94 0.19", $"UI_GetAttachment");
            }
            UI.CreateButton(ref element, PanelMessage, UIColors["red"], GetLang("Delete", player), 10, "0.06 0.05", "0.16 0.1", $"UI_DeleteMessage");
            CuiHelper.AddUi(player, element);
        }

        #endregion

        #region Console Commands

        [ConsoleCommand("UI_OpenMessage")]
        private void cmdUI_OpenMessage(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            if (!UIinfo.ContainsKey(player.userID) || !data.mail.ContainsKey(player.userID)) return;
            var index = Convert.ToInt32(arg.Args[0]);
            var i = 0;
            Mail mail = new Mail();
            foreach (var entry in data.mail[player.userID])
            {
                if (index == i)
                {
                    mail = entry;
                    entry.read = true;
                    MessageScreen(player, mail);
                    break;
                }
                else
                {
                    i++;
                    continue;
                }
            }
        }

        [ConsoleCommand("UI_ChangeMailPage")]
        private void cmdUI_ChangeMailPage(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            var page = Convert.ToInt32(arg.Args[0]);
            UIinfo[player.userID].page = page;
            MailBoxScreen(player);
        }

        [ConsoleCommand("UI_SelectRecipient")]
        private void cmdUI_SelectRecipient(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            CuiHelper.DestroyUi(player, PanelMessage);
            CuiHelper.DestroyUi(player, PanelMail);
            var element = UI.CreateElementContainer(PanelMail, UIColors["dark"], "0.3 0.3", "0.7 0.9", true);
            UI.CreatePanel(ref element, PanelMail, UIColors["light"], "0.01 0.02", "0.99 0.98");
            UI.CreateTextOutline(ref element, PanelMail, UIColors["black"], UIColors["white"], GetLang("SelectRecipient", player), 12, "0 .95", "1 1");
            var i = 0;
            foreach (BasePlayer p in BasePlayer.activePlayerList.Where(k => k.userID != player.userID))
            {
                var pos = CalcPlayerButton(i);
                if (!configData.PhysicalMailBox)
                {
                    UI.CreateButton(ref element, PanelMail, UIColors["buttongrey"], p.displayName, 10, $"{ pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"UI_ComposeMail {p.userID}");
                    i++;
                }
                else if (data.Mailboxes.ContainsValue(player.userID))
                {
                    UI.CreateButton(ref element, PanelMail, UIColors["buttongrey"], p.displayName, 10, $"{ pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"UI_ComposeMail {p.userID}");
                    i++;
                }
            }
            foreach (var entry in data.SavedPlayers.Where(k => !BasePlayer.activePlayerList.Contains(BasePlayer.FindByID(k.Key))))
            {
                var pos = CalcPlayerButton(i);
                if (!configData.PhysicalMailBox)
                {
                    UI.CreateButton(ref element, PanelMail, UIColors["buttongrey"], entry.Value.displayname, 8, $"{ pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"UI_ComposeMail {entry.Key}");
                    i++;
                }
                else if (data.Mailboxes.ContainsValue(player.userID))
                {
                    UI.CreateButton(ref element, PanelMail, UIColors["buttongrey"], entry.Value.displayname, 8, $"{ pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"UI_ComposeMail {entry.Key}");
                    i++;
                }
            }
            UI.CreateButton(ref element, PanelMail, UIColors["red"], GetLang("Close", player), 10, "0.06 0.05", "0.16 0.1", $"UI_CloseUI");
            CuiHelper.AddUi(player, element);
        }

        [ConsoleCommand("UI_ComposeMail")]
        private void cmdUI_ComposeMail(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            ulong target = Convert.ToUInt64(arg.Args[0]);
            if (!isAuth(player) && !permission.UserHasPermission(player.UserIDString, "AbsolutMail.admin"))
            if (data.mail.ContainsKey(target))
                if (!permission.UserHasPermission(target.ToString(), "AbsolutMail.admin"))
                {
                    int limit = configData.MailLimit;
                    if (permission.UserHasPermission(target.ToString(), "AbsolutMail.VIP1")) limit = configData.VIP1Limit;
                    if (permission.UserHasPermission(target.ToString(), "AbsolutMail.VIP2")) limit = configData.VIP2Limit;
                    if (permission.UserHasPermission(target.ToString(), "AbsolutMail.VIP3")) limit = configData.VIP3Limit;
                    if (data.mail[target].Count >= limit) GetSendMSG(player, "TargetMailBoxLimit");
                }
            UIinfo[player.userID].Composing = true;
            UIinfo[player.userID].CurrentMail = new Mail { recipient = target, sender = player.displayName, read = false, attachment = new MailAttachment() };
            CuiHelper.DestroyUi(player, PanelMail);
            CuiHelper.DestroyUi(player, PanelMessage);
            var element = UI.CreateElementContainer(PanelMail, "0 0 0 0", ".35 .3", ".65 .5");
            UI.CreateTextOutline(ref element, PanelMail, UIColors["black"], UIColors["white"], GetLang("ProvideSubject", player), 18, $"0 0", "1 1", TextAnchor.MiddleCenter);
            CuiHelper.AddUi(player, element);
        }


        [ConsoleCommand("UI_CloseUI")]
        private void cmdUI_CloseUI(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            CuiHelper.DestroyUi(player, PanelMail);
            CuiHelper.DestroyUi(player, PanelMessage);
        }

        [ChatCommand("mail")]
        private void chatMail(BasePlayer player, string command, string[] args)
        {
            if (args == null || args.Length == 0)
            {
                if (configData.PhysicalMailBox)
                    GetSendMSG(player, "CreateMailBoxInstructions", configData.MailKeyBinding);
                else
                    MailBoxScreen(player);
            }
            else
            {
                if (args[0] == "new")
                {
                    if (configData.PhysicalMailBox)
                        InitializeMailbox(player);
                }
                if (args[0] == "theme")
                    if (args.Length == 2)
                    {
                        AddImage(args[1], $"{player.displayName}", (ulong)ResourceId);
                        data.SavedPlayers[player.userID].theme = args[1];
                    }
                    else GetSendMSG(player, "SetThemeInstructions");
            }
        }

        [ConsoleCommand("UI_GetAttachment")]
        private void cmdUI_GetAttachment(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null || !UIinfo.ContainsKey(player.userID) || UIinfo[player.userID].CurrentMail == null || UIinfo[player.userID].CurrentMail.attachment == null)
                return;
            var mailattachment = UIinfo[player.userID].CurrentMail.attachment;
            var definition = ItemManager.FindItemDefinition(mailattachment.shortname);
            if (definition != null)
            {
                Item NewItem = ItemManager.Create(definition, mailattachment.amount, mailattachment.skin);
                if (NewItem != null)
                {
                    NewItem.condition = mailattachment.condition;
                    var held = NewItem.GetHeldEntity() as BaseProjectile;
                    if (held != null)
                    {
                        List<Item> attachments = new List<Item>();
                        if (mailattachment.weapon)
                        {
                            held.primaryMagazine.contents = mailattachment.ammoAmount;
                            held.primaryMagazine.ammoType = ItemManager.FindItemDefinition(mailattachment.ammo);
                            if (mailattachment.itemMods != null && mailattachment.itemMods.Count > 0)
                            {
                                foreach (var mod in mailattachment.itemMods)
                                {
                                    Item newMod = ItemManager.CreateByItemID((int)mod.Key, 1);
                                    newMod.condition = Convert.ToSingle(mod.Value);
                                    newMod.MoveToContainer(NewItem.contents, -1, false);
                                }
                            }
                        }
                    }
                    if (!player.inventory.containerBelt.IsFull())
                    {
                        NewItem.MoveToContainer(player.inventory.containerBelt);
                    }
                    else if (!player.inventory.containerMain.IsFull())
                        NewItem.MoveToContainer(player.inventory.containerMain);
                }
                UIinfo[player.userID].CurrentMail.attachment = null;
            }
            MessageScreen(player, UIinfo[player.userID].CurrentMail);
            return;
        }

        [ConsoleCommand("cmdMail")]
        private void cmdMail(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            if (!configData.PhysicalMailBox)
                chatMail(player, "mail", null);
            else
            {
                var sign = FindEntityFromRay(player);
                if (sign != null)
                {
                    var owner = (ulong)sign;
                    if (!data.Mailboxes.ContainsValue(owner)) return;
                    if (owner == player.userID)
                        MailBoxScreen(player);
                }
            }
        }

        [ConsoleCommand("UI_OpenMail")]
        private void cmdUI_OpenMail(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            MailBoxScreen(player);
        }

        [ConsoleCommand("UI_AttachItem")]
        private void cmdUI_AttachItem(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            if (arg.Args[0] == "no")
            {
                UIinfo[player.userID].CurrentMail.attachment = null;
                SendMail(player, UIinfo[player.userID].CurrentMail);
            }
            else if (arg.Args[0] == "yes")
                GetAttachmentItem(player);
        }

        [ConsoleCommand("UI_DeleteMessage")]
        private void cmdUI_DeleteMessage(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            //if (!UIinfo.ContainsKey(player.userID) || UIinfo[player.userID].CurrentMail == null || !data.mail.ContainsKey(player.userID)) return;
            Mail mail = UIinfo[player.userID].CurrentMail;
            data.mail[player.userID].Remove(mail);
            CuiHelper.DestroyUi(player, PanelMessage);
            MailBoxScreen(player);
        }

        [ConsoleCommand("UI_SelectItem")]
        private void cmdUI_SelectItem(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            uint id = Convert.ToUInt32(arg.Args[0]);
            var attachment = new MailAttachment { itemMods = new Dictionary<int, float>()};
            bool isWeapon = false;
            bool successful = false;
            foreach (var entry in player.inventory.containerMain.itemList.Where(k => k.uid == id))
            {
                if (entry.GetHeldEntity() != null && entry.GetHeldEntity() is BaseProjectile)
                    isWeapon = true;
                if (isWeapon)
                {
                    attachment.ammo = (entry.GetHeldEntity() as BaseProjectile).primaryMagazine.ammoType.shortname;
                    attachment.ammoAmount = (entry.GetHeldEntity() as BaseProjectile).primaryMagazine.contents;
                    if (entry.contents != null && entry.contents.itemList.Count > 0)
                    {
                        foreach (var mod in entry.contents.itemList)
                            attachment.itemMods.Add(mod.info.itemid, mod.condition);
                    }
                }
                attachment.shortname = entry.info.shortname;
                attachment.skin = entry.skin;
                attachment.weapon = isWeapon;
                attachment.amount = entry.amount;
                attachment.condition = entry.condition;
                UIinfo[player.userID].CurrentMail.attachment = attachment;
                successful = true;
                if (successful)
                {
                    SendMail(player, UIinfo[player.userID].CurrentMail);
                    entry.RemoveFromContainer();
                }
                else
                {
                    GetSendMSG(player, "SomethingWrong");
                    UIinfo[player.userID].CurrentMail = null;
                    UIinfo[player.userID].Composing = false;
                }
                break;
            }
        }


        #endregion

        #region UI Calculations

        private float[] CalcButtonPos(int number)
        {
            Vector2 position = new Vector2(0.03f, 0.75f);
            Vector2 dimensions = new Vector2(0.15f, 0.15f);
            float offsetY = 0;
            float offsetX = 0;
            if (number >= 0 && number < 6)
            {
                offsetX = (0.01f + dimensions.x) * number;
            }
            if (number > 5 && number < 12)
            {
                offsetX = (0.01f + dimensions.x) * (number - 6);
                offsetY = (-0.002f - dimensions.y) * 1;
            }
            if (number > 11 && number < 18)
            {
                offsetX = (0.01f + dimensions.x) * (number - 12);
                offsetY = (-0.002f - dimensions.y) * 2;
            }
            if (number > 17 && number < 24)
            {
                offsetX = (0.01f + dimensions.x) * (number - 18);
                offsetY = (-0.002f - dimensions.y) * 3;
            }
            if (number > 23 && number < 30)
            {
                offsetX = (0.01f + dimensions.x) * (number - 24);
                offsetY = (-0.002f - dimensions.y) * 4;
            }
            Vector2 offset = new Vector2(offsetX, offsetY);
            Vector2 posMin = position + offset;
            Vector2 posMax = posMin + dimensions;
            return new float[] { posMin.x, posMin.y, posMax.x, posMax.y };
        }

        private float[] CalcPlayerButton(int number)
        {
            Vector2 position = new Vector2(0.05f, 0.9f);
            Vector2 dimensions = new Vector2(0.12f, 0.05f);
            float offsetY = 0;
            float offsetX = 0;
            if (number >= 0 && number < 7)
            {
                offsetX = (0.01f + dimensions.x) * number;
            }
            if (number > 6 && number < 14)
            {
                offsetX = (0.01f + dimensions.x) * (number - 7);
                offsetY = (-0.01f - dimensions.y) * 1;
            }
            if (number > 13 && number < 21)
            {
                offsetX = (0.01f + dimensions.x) * (number - 14);
                offsetY = (-0.01f - dimensions.y) * 2;
            }
            if (number > 20 && number < 28)
            {
                offsetX = (0.01f + dimensions.x) * (number - 21);
                offsetY = (-0.01f - dimensions.y) * 3;
            }
            if (number > 27 && number < 35)
            {
                offsetX = (0.01f + dimensions.x) * (number - 28);
                offsetY = (-0.01f - dimensions.y) * 4;
            }
            if (number > 34 && number < 42)
            {
                offsetX = (0.01f + dimensions.x) * (number - 35);
                offsetY = (-0.01f - dimensions.y) * 5;
            }
            if (number > 41 && number < 49)
            {
                offsetX = (0.01f + dimensions.x) * (number - 42);
                offsetY = (-0.01f - dimensions.y) * 6;
            }
            if (number > 48 && number < 56)
            {
                offsetX = (0.01f + dimensions.x) * (number - 49);
                offsetY = (-0.01f - dimensions.y) * 7;
            }
            if (number > 55 && number < 63)
            {
                offsetX = (0.01f + dimensions.x) * (number - 56);
                offsetY = (-0.01f - dimensions.y) * 8;
            }
            if (number > 62 && number < 70)
            {
                offsetX = (0.01f + dimensions.x) * (number - 63);
                offsetY = (-0.01f - dimensions.y) * 9;
            }
            if (number > 69 && number < 77)
            {
                offsetX = (0.01f + dimensions.x) * (number - 70);
                offsetY = (-0.01f - dimensions.y) * 10;
            }
            if (number > 76 && number < 84)
            {
                offsetX = (0.01f + dimensions.x) * (number - 77);
                offsetY = (-0.01f - dimensions.y) * 11;
            }
            if (number > 83 && number < 91)
            {
                offsetX = (0.01f + dimensions.x) * (number - 84);
                offsetY = (-0.01f - dimensions.y) * 12;
            }
            if (number > 90 && number < 98)
            {
                offsetX = (0.01f + dimensions.x) * (number - 91);
                offsetY = (-0.01f - dimensions.y) * 13;
            }
            if (number > 97 && number < 105)
            {
                offsetX = (0.01f + dimensions.x) * (number - 98);
                offsetY = (-0.01f - dimensions.y) * 14;
            }
            if (number > 104 && number < 112)
            {
                offsetX = (0.01f + dimensions.x) * (number - 105);
                offsetY = (-0.01f - dimensions.y) * 15;
            }
            if (number > 111 && number < 119)
            {
                offsetX = (0.01f + dimensions.x) * (number - 112);
                offsetY = (-0.01f - dimensions.y) * 16;
            }


            Vector2 offset = new Vector2(offsetX, offsetY);
            Vector2 posMin = position + offset;
            Vector2 posMax = posMin + dimensions;
            return new float[] { posMin.x, posMin.y, posMax.x, posMax.y };
        }



        private float[] CalcMailLocation(int number)
        {
            Vector2 position = new Vector2(0.03f, 0.87f);
            Vector2 dimensions = new Vector2(0.93f, 0.03f);
            float offsetY = 0;
            float offsetX = 0;
            //if (number >= 0 && number < 10)
            //{
                offsetY = (-0.01f - dimensions.y) * number;
            //}
            //if (number > 7 && number < 16)
            //{
            //    offsetX = (0.01f + dimensions.x) * 1;
            //    offsetY = (-0.01f - dimensions.y) * (number - 8);
            //}
            //if (number > 15 && number < 24)
            //{
            //    offsetX = (0.01f + dimensions.x) * 1;
            //    offsetY = (-0.01f - dimensions.y) * (number - 16);
            //}
            Vector2 offset = new Vector2(offsetX, offsetY);
            Vector2 posMin = position + offset;
            Vector2 posMax = posMin + dimensions;
            return new float[] { posMin.x, posMin.y, posMax.x, posMax.y };
        }

        #endregion

        #region Classes
        class SavedData
        {
            public Dictionary<uint, ulong> Mailboxes = new Dictionary<uint, ulong>();
            public Dictionary<ulong, Player> SavedPlayers = new Dictionary<ulong, Player>();
            public Dictionary<ulong, List<Mail>> mail = new Dictionary<ulong, List<Mail>>();
        }
        class Player
        {
            public string displayname;
            public string theme;
        }
        class Mail
        {
            public string subject;
            public string message;
            public MailAttachment attachment;
            public string sender;
            public bool read;
            public ulong recipient;
        }
        class MailAttachment
        {
            public string shortname;
            public ulong skin;
            public int amount;
            public bool weapon;
            public string ammo;
            public int ammoAmount;
            public Dictionary<int, float> itemMods = new Dictionary<int, float>();
            public float condition;
        }
        #endregion

        #region Data Management
        void SaveData()
        {
            Data.WriteObject(data);
        }

        void LoadData()
        {
            try
            {
                data = Data.ReadObject<SavedData>();
                if (data == null)
                {
                    Puts("Corrupt Data file....creating new datafile");
                    data = new SavedData();


                }
            }
            catch
            {

                Puts("Couldn't load AbsolutMail Data, creating new datafile");
                data = new SavedData();
            }
        }
        #endregion

        #region Config        
        private ConfigData configData;
        class ConfigData
        {
            public bool PhysicalMailBox { get; set; }
            public int InfoInterval { get; set; }
            public string MailKeyBinding { get; set; }
            public bool UseMailBoxIcon { get; set; }
            public bool UseThemes { get; set; }
            public int MailLimit { get; set; }
            public bool AllowAttachments { get; set; }
            public int VIP1Limit { get; set; }
            public int VIP2Limit { get; set; }
            public int VIP3Limit { get; set; }
            public int MailBoxCooldown { get; set; }
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
                PhysicalMailBox = true,
                UseMailBoxIcon = true,
                UseThemes = true,
                InfoInterval = 10,
                MailKeyBinding = "n",
                MailLimit = 10,
                VIP1Limit = 10,
                VIP2Limit = 10,
                VIP3Limit = 10,
                AllowAttachments = true,
                MailBoxCooldown = 5,
            };
            SaveConfig(config);
        }
        private void LoadConfigVariables() => configData = Config.ReadObject<ConfigData>();
        void SaveConfig(ConfigData config) => Config.WriteObject(config, true);
        #endregion

        #region Messages
        Dictionary<string, string> messages = new Dictionary<string, string>()
        {
            {"title", "AboslutMail: " },
            {"MailInfo", "This server is running AbsolutMail. To access mail {0}"},
            {"PhysicalInfo","create a mail box with '/mail new' and interact with the mail system by pressing '{0}' while facing the mailbox." },
            {"VirtualInfo","type '/mail'." },
            {"ThemeInfo", "You can also set a custom theme by typing /mail theme TheURLofThemeImage" },
            {"AttachAnItem", "Would You Like to Attach an Item?" },
            {"ComposingEnded", "You are no longer composing a new mail"},
            {"ProvideMessage", "Please provide a message for in the mail or type 'quit' to stop composing a new mail." },
            {"ProvideSubject", "Please provide a subject for the mail or type 'quit' to stop composing a new mail." },
            {"SomethingWrong", "Something went wrong when preparing the mail. Process halt. " },
            {"SuccessfulMail", "Your message has been sent!" },
            {"SelectRecipient", "Select a recipient" },
            {"NotAMailbox", "This is not a mailbox" },
            {"Attachment", "Attachment" },
            {"Sender", "Sent by: {0}" },
            {"MailBox", "MailBox" },
            {"CreateMailBoxInstructions", "To access your mailbox create a mailbox by typing '/mail new'. Once created you can interact with the mail system by pressing '{0}' while facing your mailbox." },
            {"SetThemeInstructions", "To set a mailbox theme simply type /mail theme 'url of image'. For example if the picture you want is at www.something.com.. type /mail theme www.something.com" },
            {"TargetMailBoxLimit", "The target recipients mailbox is full. Tell them to delete something to make room." },
            {"NotInTCRadius", "Unable to make a mailbox here. You must be in the radius of an Authorized ToolCupboard." },
            {"MailboxCooldown", "You are on a MailBox creation cooldown of {0} minutes. Please try later." }
        };
        #endregion
    }
}