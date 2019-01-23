// Requires: FactionsCore
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
    [Info("FactionsTax", "Absolut", "1.0.0", ResourceId = 2424)]
    [Description("Applies a Tax to killed players. If the killing Faction has a Tax Box it will automatically collect a tax. Taxes expire after a period of time defined in the config.")]
    class FactionsTax : RustPlugin
    {
        #region Fields

        [PluginReference]
        FactionsCore FactionsCore;

        string TitleColor = "<color=orange>";
        string MsgColor = "<color=#A9A9A9>";

        FactionTaxData ftData;
        private DynamicConfigFile FTDATA;

        private Vector3 eyesAdjust;

        private Dictionary<string, Timer> timers = new Dictionary<string, Timer>();
        private Dictionary<ulong, XYZ> BoxPrep = new Dictionary<ulong, XYZ>();
        private List<ulong> VoidSelection = new List<ulong>();
        static FieldInfo buildingPrivlidges = typeof(BasePlayer).GetField("buildingPrivilege", BindingFlags.Instance | BindingFlags.NonPublic);

        #endregion

        #region Server Hooks

        void Loaded()
        {
            FTDATA = Interface.Oxide.DataFileSystem.GetFile("FactionTax_Data");
            lang.RegisterMessages(messages, this);
        }

        void AddTax(ushort TaxingFaction, ulong payer)
        {
            if (!ftData.TaxPayers.ContainsKey(TaxingFaction))
                ftData.TaxPayers.Add(TaxingFaction, new List<ulong>());
            if (!ftData.TaxPayers[TaxingFaction].Contains(payer))
                ftData.TaxPayers[TaxingFaction].Add(payer);
            int index = ftData.TaxTimers.Count() == 0 ? 0 : ftData.TaxTimers.Keys.Max() + 1;
            if (timers.ContainsKey(index.ToString()))
            {
                timers[index.ToString()].Destroy();
                timers.Remove(index.ToString());
            }
            ftData.TaxTimers.Add(index, new PlayerTimers { Faction = TaxingFaction, ResetTime = GrabCurrentTime() + (configData.TaxTimeLimit * 3600), TaxPayer = payer });
            timers.Add(index.ToString(), timer.Once((float)ftData.TaxTimers[index].ResetTime, () => RemoveTax(index)));
        }

        static double GrabCurrentTime() => DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1, 0, 0, 0)).TotalSeconds;

        void RemoveTax(int index)
        {
            if (timers.ContainsKey(index.ToString()))
            {
                timers[index.ToString()].Destroy();
                timers.Remove(index.ToString());
            }
            if (ftData.TaxTimers.ContainsKey(index))
            {
                var data = ftData.TaxTimers[index];
                if (ftData.TaxPayers.ContainsKey(data.Faction) && ftData.TaxPayers[data.Faction].Contains(data.TaxPayer))
                    ftData.TaxPayers[data.Faction].Remove(data.TaxPayer);
                ftData.TaxTimers.Remove(index);
            }
        }

        void Unload()
        {
            BoxPrep.Clear();
            VoidSelection.Clear();
            foreach (var entry in timers)
                entry.Value.Destroy();
            timers.Clear();
            SaveData();
        }

        private void OnPlayerDisconnected(BasePlayer player)
        {
            if (VoidSelection.Contains(player.userID))
                VoidSelection.Remove(player.userID);
            if (BoxPrep.ContainsKey(player.userID))
                BoxPrep.Remove(player.userID);
        }

        void OnServerInitialized()
        {
            LoadVariables();
            LoadData();
            foreach (var entry in ftData.TaxTimers)
            {
                if (timers.ContainsKey(entry.Key.ToString()))
                {
                    timers[entry.Key.ToString()].Destroy();
                    timers.Remove(entry.Key.ToString());
                }
                if (GrabCurrentTime() > ftData.TaxTimers[entry.Key].ResetTime)
                    RemoveTax(entry.Key);
                else timers.Add(entry.Key.ToString(), timer.Once((float)ftData.TaxTimers[entry.Key].ResetTime, () => RemoveTax(entry.Key)));
            }
            eyesAdjust = new Vector3(0f, 1.5f, 0f);
            InfoLoop();
            SaveLoop();
        }

        #endregion

        #region Player Hooks

        private void OnEntityDeath(BaseEntity entity, HitInfo hitInfo)
        {
            if (hitInfo == null) return;
            if (entity is StorageContainer)
            {
                Vector3 ContPosition = entity.transform.position;
                if (ftData.TaxBox.ContainsValue(new XYZ { x = ContPosition.x, y = ContPosition.y, z = ContPosition.z }))
                {
                    List<ushort> boxOwners = ftData.TaxBox.Where(k => k.Value == new XYZ { x = ContPosition.x, y = ContPosition.y, z = ContPosition.z }).Select(k => k.Key).ToList();
                    ftData.TaxBox.Remove(boxOwners[0]);
                    FactionsCore.BroadcastFaction(null, "TaxBoxDestroyed", boxOwners[0]);
                    SaveData();
                }
                return;
            }
            var victim = entity.ToPlayer();
            var attacker = hitInfo.Initiator.ToPlayer() as BasePlayer;
            if (victim == null || attacker == null) return;
            if (GetFactionPlayer(attacker) == null || GetFactionPlayer(attacker).Faction == 0) return;
            if (victim.userID != attacker.userID)
            {
                AddTax(GetFactionPlayer(attacker).Faction, victim.userID);
                SaveData();
            }
        }

        void OnPlantGather(PlantEntity Plant, Item item, BasePlayer player)
        {
            List<StorageContainer> TaxContainers = GetTaxContainer(player.userID);
            if (TaxContainers == null) return;
            var taxrate = configData.TaxRate;
            int taxcollectors = TaxContainers.Count();
            var maxtaxors = Math.Floor(100 / taxrate);
            if (maxtaxors < taxcollectors)
                taxrate = 90 / taxcollectors;

            int Tax = Convert.ToInt32(Math.Floor((item.amount * taxrate) / 100));
            item.amount = item.amount - (Tax * taxcollectors);
            foreach (StorageContainer cont in TaxContainers)
            {
                if (!cont.inventory.IsFull())
                {
                    ItemDefinition ToAdd = ItemManager.FindItemDefinition(item.info.itemid);
                    if (ToAdd != null)
                    {
                        cont.inventory.AddItem(ToAdd, Tax);
                    }
                }
                else
                {
                    item.amount += Tax;
                    List<ushort> boxOwners = ftData.TaxBox.Where(k => k.Value == new XYZ { x = cont.transform.localPosition.x, y = cont.transform.localPosition.y, z = cont.transform.localPosition.z }).Select(k => k.Key).ToList();
                    if (timers.ContainsKey(boxOwners[0].ToString()))
                    {
                        FactionsCore.BroadcastFaction(null, "TaxBoxFull", boxOwners[0]);
                        SetBoxFullNotification(boxOwners[0].ToString());
                        return;
                    }
                }
            }
        }


        void OnCollectiblePickup(Item item, BasePlayer player)
        {
            List<StorageContainer> TaxContainers = GetTaxContainer(player.userID);
            if (TaxContainers == null) return;
            var taxrate = configData.TaxRate;
            int taxcollectors = TaxContainers.Count();
            var maxtaxors = Math.Floor(100 / taxrate);
            if (maxtaxors < taxcollectors)
                taxrate = 90 / taxcollectors;

            int Tax = Convert.ToInt32(Math.Floor((item.amount * taxrate) / 100));
            item.amount = item.amount - (Tax * taxcollectors);
            foreach (StorageContainer cont in TaxContainers)
            {
                if (!cont.inventory.IsFull())
                {
                    ItemDefinition ToAdd = ItemManager.FindItemDefinition(item.info.itemid);
                    if (ToAdd != null)
                    {
                        cont.inventory.AddItem(ToAdd, Tax);
                    }
                }
                else
                {
                    item.amount += Tax;
                    List<ushort> boxOwners = ftData.TaxBox.Where(k => k.Value == new XYZ { x = cont.transform.localPosition.x, y = cont.transform.localPosition.y, z = cont.transform.localPosition.z }).Select(k => k.Key).ToList();
                    if (timers.ContainsKey(boxOwners[0].ToString()))
                    {
                        FactionsCore.BroadcastFaction(null, "TaxBoxFull", boxOwners[0]);
                        SetBoxFullNotification(boxOwners[0].ToString());
                        return;
                    }
                }
            }
        }

        void OnDispenserGather(ResourceDispenser Dispenser, BaseEntity entity, Item item)
        {
            BasePlayer player = entity.ToPlayer();
            List<StorageContainer> TaxContainers = GetTaxContainer(player.userID);
            if (TaxContainers == null) return;
            var taxrate = configData.TaxRate;
            int taxcollectors = TaxContainers.Count();
            var maxtaxors = Math.Floor(100 / taxrate);
            if (maxtaxors < taxcollectors)
                taxrate = 90 / taxcollectors;
            int Tax = Convert.ToInt32(Math.Floor((item.amount * taxrate) / 100));
            item.amount = item.amount - (Tax * taxcollectors);
            foreach (StorageContainer cont in TaxContainers)
            {
                if (!cont.inventory.IsFull())
                {
                    ItemDefinition ToAdd = ItemManager.FindItemDefinition(item.info.itemid);
                    if (ToAdd != null)
                    {
                        cont.inventory.AddItem(ToAdd, Tax);
                    }
                }
                else
                {
                    item.amount += Tax;
                    List<ushort> boxOwners = ftData.TaxBox.Where(k => k.Value == new XYZ { x = cont.transform.localPosition.x, y = cont.transform.localPosition.y, z = cont.transform.localPosition.z }).Select(k => k.Key).ToList();
                    if (timers.ContainsKey(boxOwners[0].ToString()))
                    {
                        FactionsCore.BroadcastFaction(null, "TaxBoxFull", boxOwners[0]);
                        SetBoxFullNotification(boxOwners[0].ToString());
                        return;
                    }
                }
            }
        }


        #endregion

        #region Functions
        public object isPayor(ulong ID)
        {
            if (ftData.TaxPayers == null || ftData.TaxPayers.Count == 0) return false;
            List<ushort> TaxingFactions = new List<ushort>();
            foreach (var entry in ftData.TaxPayers)
                    if (entry.Value.Contains(ID)) TaxingFactions.Add(entry.Key);
            if (TaxingFactions.Count > 0) return TaxingFactions;
            return false;
        }
        double GetTaxRate()
        {
            return configData.TaxRate;
        }

        List<StorageContainer> GetTaxContainers(ulong Payor)
        {
           return GetTaxContainer(Payor);
        }

        private List<StorageContainer> GetTaxContainer(ulong Payor)
        {
            if (isPayor(Payor) is bool) return null;
            List<StorageContainer> Containers = new List<StorageContainer>();
            foreach (var entry in (List<ushort>)isPayor(Payor))
                if (ftData.TaxBox.ContainsKey(entry))
                {
                    Vector3 containerPos = GetVector3(ftData.TaxBox[entry]);
                    foreach (StorageContainer Cont in StorageContainer.FindObjectsOfType<StorageContainer>())
                    {
                        Vector3 ContPosition = Cont.transform.position;
                        if (ContPosition == containerPos)
                            Containers.Add(Cont);
                    }
                }
            if (Containers.Count > 0)
                return Containers;
            else return null;
        }

        Vector3 GetVector3(XYZ xyz)
        {
            return new Vector3 { x = xyz.x, y = xyz.y, z = xyz.z };
        }

        private void GetSendMSG(BasePlayer player, string message, string arg1 = "", string arg2 = "", string arg3 = "", string arg4 = "")
        {
            string msg = string.Format(lang.GetMessage(message, this, player.UserIDString), arg1, arg2, arg3, arg4);
            SendReply(player, TitleColor + lang.GetMessage("title", this, player.UserIDString) + "</color>" + MsgColor + msg + "</color>");
        }

        private string GetMSG(string message, BasePlayer player = null, string arg1 = "", string arg2 = "", string arg3 = "", string arg4 = "")
        {
            string p = null;
            if (player != null)
                p = player.UserIDString;
            if (messages.ContainsKey(message))
                return string.Format(lang.GetMessage(message, this, p), arg1, arg2, arg3, arg4);
            else return message;
        }

        public void DestroyTaxPanel(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, PanelTax);
        }
        #endregion

        #region UI Creation

        private string PanelTax = "Tax";
        private string PanelOnScreen = "PanelOnScreen";

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

            static public void LoadImage(ref CuiElementContainer container, string panel, string png, string aMin, string aMax)
            {
                container.Add(new CuiElement
                {
                    Parent = panel,
                    Components =
                    {
                        new CuiRawImageComponent {Png = png },
                        new CuiRectTransformComponent {AnchorMin = aMin, AnchorMax = aMax }
                    }
                });
            }
            static public void CreateTextOverlay(ref CuiElementContainer container, string panel, string text, string color, int size, string aMin, string aMax, TextAnchor align = TextAnchor.MiddleCenter, float fadein = 1.0f)
            {
                //if (configdata.DisableUI_FadeIn)
                //    fadein = 0;
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
            {"white", "1 1 1 1" },
        };
        #endregion

        #region UI Panels

        void OnScreen(BasePlayer player, string msg, string arg1 = "", string arg2 = "", string arg3 = "")
        {
            if (timers.ContainsKey(player.userID.ToString()))
            {
                timers[player.userID.ToString()].Destroy();
                timers.Remove(player.userID.ToString());
            }
            CuiHelper.DestroyUi(player, PanelOnScreen);
            var element = UI.CreateOverlayContainer(PanelOnScreen, "0.0 0.0 0.0 0.0", "0.15 0.85", "0.85 .95", false);
            UI.CreateTextOutline(ref element, PanelOnScreen, UIColors["white"], UIColors["black"], GetMSG(msg, player, arg1, arg2, arg3), 24, "0.0 0.0", "1.0 1.0");
            CuiHelper.AddUi(player, element);
            timers.Add(player.userID.ToString(),timer.Once(4, () => CuiHelper.DestroyUi(player, PanelOnScreen)));
        }

        #endregion

        #region  External Functions
        public FactionsCore.FactionPlayer GetFactionPlayer(BasePlayer player)
        {
            if (!player.GetComponent<FactionsCore.FactionPlayer>()) return null;
            else return player.GetComponent<FactionsCore.FactionPlayer>();
        }

        public object GetFactionInfo(ushort ID, string request)
        {
            var result = FactionsCore.Call("GetFactionInfo", ID, request);
            if (result is bool || result == null) return false;
            if (result is IList<ulong> && result.GetType().IsGenericType && result.GetType().GetGenericTypeDefinition().IsAssignableFrom(typeof(List<>))) return (List<ulong>)result;
            if (result is ulong) return (ulong)result;
            if (result is string) return (string)result;
            return false;
        }

        [HookMethod("HasTaxBox")]
        bool HasTaxBox(ushort ID)
        {
            if (ftData.TaxBox.ContainsKey(ID)) return true;
            return false;
        }

        [HookMethod("TaxboxLocation")]
        object TaxboxLocation(ushort ID)
        {
            if (!HasTaxBox(ID)) return false;
            return (GetVector3(ftData.TaxBox[ID]));
        }
        #endregion

        #region UI Commands

        [ConsoleCommand("UI_SetTaxBox")] ///USED IN FACTIONS CORE
        private void cmdUI_SetTaxBox(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null || GetFactionPlayer(player) == null || GetFactionPlayer(player).Faction == 0) return;
            ushort faction = GetFactionPlayer(player).Faction;
            ulong owner = 0L;
            List<ulong> moderators = new List<ulong>();
            var result1 = GetFactionInfo(faction, "owner");
            if (result1 is ulong)
                owner = (ulong)result1;
            var result2 = GetFactionInfo(faction, "moderators");
            if (result2 is IList<ulong> && result2.GetType().IsGenericType && result2.GetType().GetGenericTypeDefinition().IsAssignableFrom(typeof(List<>)))
                moderators = (List<ulong>)result2;
            if (!moderators.Contains(player.userID) && owner != player.userID) return;
            var hit = FindEntityFromRay(player);
            if (hit != null)
            {
                StorageContainer box = hit as StorageContainer;
                if (ftData.TaxBox.ContainsKey(faction))
                    ftData.TaxBox.Remove(faction);
                ftData.TaxBox.Add(faction, new XYZ { x = box.transform.localPosition.x, y = box.transform.localPosition.y, z = box.transform.localPosition.z });
                OnScreen(player, "NewTaxBox");
                SaveData();
            }
            else OnScreen(player, "NoBoxFound");

        }

        object FindEntityFromRay(BasePlayer player)
        {
            Ray ray = new Ray(player.eyes.position, Quaternion.Euler(player.input.state.current.aimAngles) * Vector3.forward);
            RaycastHit hit;
            if (!Physics.Raycast(ray, out hit, 20))
                return null;
            var hitEnt = hit.collider.GetComponentInParent<StorageContainer>();
            if (hitEnt != null)
                if (AllowedToBuild(player))
                    return hitEnt as StorageContainer;
                else OnScreen(player, "TaxBoxError");
            else OnScreen(player, "NoBox");
            return null;
        }

        static bool AllowedToBuild(BasePlayer player)
        {
            if (player == null) return false;
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
            foreach (BasePlayer p in BasePlayer.activePlayerList)
            {
                GetSendMSG(p, "FactionTaxInfo", configData.TaxTimeLimit.ToString());
            }
            timers.Add("info",timer.Once(900, () => InfoLoop()));
        }

        private void SetBoxFullNotification(string ID)
        {
            timers.Add(ID, timer.Once(5 * 60, () => timers.Remove(ID)));
        }

        #endregion

        #region Classes
        class FactionTaxData
        {
            public Dictionary<ushort, List<ulong>> TaxPayers = new Dictionary<ushort, List<ulong>>();
            public Dictionary<int, PlayerTimers> TaxTimers = new Dictionary<int, PlayerTimers>();
            public Dictionary<ushort, XYZ> TaxBox = new Dictionary<ushort, XYZ>();
        }

        class PlayerTimers
        {
            public double ResetTime;
            public ushort Faction;
            public ulong TaxPayer;
        }

        class XYZ
        {
            public float x;
            public float y;
            public float z;
        }
        #endregion

        #region Data Management

        void SaveData()
        {
            FTDATA.WriteObject(ftData);
        }

        void LoadData()
        {
            try
            {
                ftData = FTDATA.ReadObject<FactionTaxData>();
                if(ftData == null)
                    ftData = new FactionTaxData();
            }
            catch
            {
                Puts("Couldn't load FactionsTax data, creating new datafile");
                ftData = new FactionTaxData();
            }
            if (ftData.TaxBox == null)
                ftData.TaxBox = new Dictionary<ushort, XYZ>();
            if (ftData.TaxPayers == null)
                ftData.TaxPayers = new Dictionary<ushort, List<ulong>>();
            if (ftData.TaxTimers == null)
                ftData.TaxTimers = new Dictionary<int, PlayerTimers>();
        }
        #endregion

        #region Config        
        private ConfigData configData;
        class ConfigData
        {
            public double TaxRate { get; set; }
            public int TaxTimeLimit { get; set; }
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
                TaxRate = 5,
                TaxTimeLimit = 8,
            };
            SaveConfig(config);
        }
        private void LoadConfigVariables() => configData = Config.ReadObject<ConfigData>();
        void SaveConfig(ConfigData config) => Config.WriteObject(config, true);
        #endregion

        #region Messages
        Dictionary<string, string> messages = new Dictionary<string, string>()
        {
            {"title", "FactionTax: " },
            {"FactionTaxInfo", "This server is running Factions Tax. Each player you kill will be required to pay a tax to your Faction Box for {0} hours."},
            {"NoBoxPrepped", "Error finding target tax box!" },
            {"TaxBoxDestroyed", "Your tax box has been destroyed!" },
            {"TaxBoxFull", "Your Faction tax box is full! Clear room to generate taxes." },
            {"NewTaxBox", "You have set a new Tax Box" },
            {"TaxRemoved", "You are no longering being taxed by {0}!" },
            {"NoBox", "You are not looking at a Storage Box!" },
            {"TaxBoxError", "You can only set a Tax Box in an authorized build zone." },
            {"NoBoxFound", "You are not looking at a box!" }
        };
        #endregion
    }
}
