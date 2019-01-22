// Requires: ImageLibrary
// Reference: System.Drawing
using System;
using System.Text;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Game.Rust.Cui;
using Oxide.Core.Plugins;
using UnityEngine;
using System.Linq;
using System.Globalization;
using System.IO;
using System.Drawing;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("LustyMap", "Kayzor / k1lly0u", "2.1.39", ResourceId = 1333)]
    class LustyMap : RustPlugin
    {
        #region Fields
        [PluginReference] Plugin Clans, EventManager, Friends;
        [PluginReference] ImageLibrary ImageLibrary;

        static MapSplitter mapSplitter;

        static LustyMap instance;
        static float mapSize;
        static string mapSeed;
        static string worldSize;
        static string level;

        private bool activated;
        private bool isNewSave;
        private bool isRustFriends;        
        
        MarkerData storedMarkers;
        private DynamicConfigFile markerData;

        private Dictionary<string, MapUser> mapUsers;

        private List<MapMarker> staticMarkers;
        private Dictionary<string, MapMarker> customMarkers;
        private Dictionary<string, MapMarker> temporaryMarkers;
        private Dictionary<uint, ActiveEntity> entityMarkers;

        private Dictionary<string, List<string>> clanData;

        static string dataDirectory = $"file://{Interface.Oxide.DataDirectory}{Path.DirectorySeparatorChar}LustyMap{Path.DirectorySeparatorChar}";
        #endregion
        
        #region User Class  
        class MapUser : MonoBehaviour
        {
            private Dictionary<string, List<string>> friends;
            public HashSet<string> friendList;

            public BasePlayer player;
            public MapMode mode;
            public MapMode lastMode;

            private MapMarker marker;

            private int mapX;
            private int mapZ;

            private int currentX;
            private int currentZ;
            private int mapZoom;

            private bool mapOpen;
            private bool inEvent;
            private bool adminMode;

            private int changeCount;
            private double lastChange;
            private bool isBlocked;
            
            private ConfigData.SpamOptions spam;

            private bool afkDisabled;
            private int lastMoveTime;
            private float lastX;
            private float lastZ;


            void Awake()
            {                
                player = GetComponent<BasePlayer>();
                friends = new Dictionary<string, List<string>>
                {
                    {"Clans", new List<string>() },
                    {"FriendsAPI", new List<string>() }
                };
                friendList = new HashSet<string>();
                spam = instance.configData.Spam;             
                inEvent = false;
                mapOpen = false;
                afkDisabled = false;
                enabled = false;
                mode = MapMode.None;
                lastMode = MapMode.None;
                adminMode = false;
                InvokeHandler.InvokeRepeating(this, UpdateMarker, 0.1f, 1f);
            }
            void OnDestroy()
            {
                InvokeHandler.CancelInvoke(this, UpdateMarker);
                InvokeHandler.CancelInvoke(this, UpdateMap);
                DestroyUI();
            }
            public void InitializeComponent()
            {
                if (MapSettings.friends)
                {
                    FillFriendList();
                }                

                if (!instance.configData.Map.StartOpen)
                {
                    ToggleMapType(MapMode.None);
                    return;
                }

                if (MapSettings.complexmap)
                {
                    mapZoom = 1;
                    ToggleMapType(MapMode.Complex);

                    if (MapSettings.forcedzoom)
                        mapZoom = MapSettings.zoomlevel;
                }

                else if (MapSettings.minimap)
                {
                    mode = MapMode.Minimap;
                    ToggleMapType(mode);
                }               
            }

            #region Friends
            private void FillFriendList()
            {                
                if (instance.configData.Friends.UseClans)
                {
                    var clanTag = instance.GetClan(player.userID);
                    if (!string.IsNullOrEmpty(clanTag) && instance.clanData.ContainsKey(clanTag))                    
                        friends["Clans"] = instance.clanData[clanTag];
                }
                    
                if (instance.configData.Friends.UseFriends)
                    friends["FriendsAPI"] = instance.GetFriends(player.userID);
                UpdateMembers();
            }
            private void UpdateMembers()
            {
                friendList.Clear();
                foreach (var list in friends)
                {
                    foreach (var member in list.Value)
                        friendList.Add(member);
                }
            }
            #endregion

            #region Maps
            public float Rotation() => GetDirection(player?.transform?.rotation.eulerAngles.y ?? 0);
            public int Position(bool x) => x ? mapX : mapZ;            
            public void Position(bool x, int pos)
            {
                if (x) mapX = pos;
                else mapZ = pos;
            } 
                        
            public void ToggleMapType(MapMode mapMode)
            {
                if (isBlocked || IsSpam()) return;

                DestroyUI();               

                if (mapMode == MapMode.None)
                {
                    InvokeHandler.CancelInvoke(this, UpdateMap);                   
                    mode = MapMode.None;
                    mapOpen = false;

                    if (MapSettings.minimap)                    
                        instance.CreateShrunkUI(player);                    
                }
                else
                {               
                    mapOpen = true;
                    switch (mapMode)
                    {
                        case MapMode.Main:
                            mode = MapMode.Main;
                            instance.OpenMainMap(player);
                            break;
                        case MapMode.Complex:
                            mode = MapMode.Complex;                            
                            instance.OpenComplexMap(player);
                            break;
                        case MapMode.Minimap:
                            mode = MapMode.Minimap;
                            instance.OpenMiniMap(player);
                            break;
                    }
                    if (!IsInvoking("UpdateMap"))                    
                        InvokeHandler.InvokeRepeating(this, UpdateMap, 0.1f, instance.configData.Map.UpdateSpeed);                    
                }     
            }                      
            public void UpdateMap()
            {                
                switch (mode)
                {
                    case MapMode.None:
                        break;
                    case MapMode.Main:
                        instance.UpdateOverlay(player, LustyUI.MainOverlay, LustyUI.MainMin, LustyUI.MainMax, 0.01f);
                        break;
                    case MapMode.Complex:
                        CheckForChange();
                        instance.UpdateCompOverlay(player);
                        break;
                    case MapMode.Minimap:
                        instance.UpdateOverlay(player, LustyUI.MiniOverlay, LustyUI.MiniMin, LustyUI.MiniMax, 0.03f);
                        break;
                }
            }
            #endregion

            #region Complex
            public bool HasMapOpen() => (mapOpen && mode == MapMode.Main);
            public int Zoom() => mapZoom;
            public void Zoom(bool zoomIn)
            {
                var zoom = mapZoom;
                if (zoomIn)
                {
                    if (zoom < 3)
                        zoom++;
                    else return;
                }
                else
                {
                    if (zoom > 0)
                        zoom--;
                    else return;
                }
                InvokeHandler.CancelInvoke(this, UpdateMap);
                SwitchZoom(zoom);
            }
            private void SwitchZoom(int zoom)
            {
                if (zoom == 0 && MapSettings.minimap)
                {                    
                    mapZoom = zoom;
                    ToggleMapType(MapMode.Minimap);
                }
                else
                {
                    if (zoom == 0 && !MapSettings.minimap)
                        zoom = 1;
                    
                    mapZoom = zoom;
                    currentX = 0;
                    currentZ = 0;
                    ToggleMapType(MapMode.Complex);
                }
            }
            public int Current(bool x) => x ? currentX : currentZ;
            public void Current(bool x, int num)
            {
                if (x) currentX = num;
                else currentZ = num;
            }           
            private void CheckForChange()
            {
                var mapSlices = ZoomToCount(mapZoom);
                float x = player.transform.position.x + mapSize / 2f;
                float z = player.transform.position.z + mapSize / 2f;
                var mapres = mapSize / mapSlices;

                var newX = Convert.ToInt32(Math.Ceiling(x / mapres)) - 1;
                var newZ = mapSlices - Convert.ToInt32(Math.Ceiling(z / mapres));

                if (currentX != newX || currentZ != newZ)
                {
                    DestroyUI();
                    currentX = newX;
                    currentZ = newZ;
                    instance.OpenComplexMap(player);
                }
            }
            #endregion

            #region Spam Checking
            private bool IsSpam()
            {
                if (!spam.Enabled) return false;

                changeCount++;
                var current = GrabCurrentTime();
                if (current - lastChange < spam.TimeBetweenAttempts)
                {
                    lastChange = current;
                    if (changeCount > spam.WarningAttempts && changeCount < spam.DisableAttempts)
                    {
                        instance.SendReply(player, instance.msg("spamWarning", player.UserIDString));
                        return false;
                    }
                    if (changeCount >= spam.DisableAttempts)
                    {
                        instance.SendReply(player, string.Format(instance.msg("spamDisable", player.UserIDString), spam.DisableSeconds));
                        Block();
                        Invoke("Unblock", spam.DisableSeconds);
                        return true;
                    }
                }
                else
                {
                    lastChange = current;
                    changeCount = 0;
                }
                return false;
            }
            private void Block()
            {                
                isBlocked = true;
                OnDestroy();
            }
            private void Unblock()
            {
                isBlocked = false;
                ToggleMapType(lastMode);
                instance.SendReply(player, instance.msg("spamEnable", player.UserIDString));
            }
            #endregion

            #region Other
            public bool InEvent() => inEvent;
            public bool IsAdmin => adminMode;
            public void ToggleEvent(bool isPlaying) => inEvent = isPlaying;
            public void ToggleAdmin(bool enabled) => adminMode = enabled;
            public void DestroyUI() => LustyUI.DestroyUI(player);
            private void UpdateMarker()
            {
                var currentX = (float)Math.Round(transform.position.x, 1);
                var currentZ = (float)Math.Round(transform.position.z, 1);

                marker = new MapMarker { name = RemoveSpecialCharacters(player.displayName), r = GetDirection(player?.eyes?.rotation.eulerAngles.y ?? 0), x = GetPosition(transform.position.x), z = GetPosition(transform.position.z) };

                if (instance.configData.Map.EnableAFKTracking)
                {
                    if (lastX == currentX && lastZ == currentZ)
                        ++lastMoveTime;
                    else
                    {
                        lastX = currentX;
                        lastZ = currentZ;
                        lastMoveTime = 0;
                        if (afkDisabled)
                        {
                            afkDisabled = false;
                            EnableUser();
                        }
                    }

                    if (lastMoveTime == 90)
                    {
                        afkDisabled = true;
                        DisableUser();
                    }
                }              
            }
            public MapMarker GetMarker() => marker;
            
            public void ToggleMain()
            {
                if (HasMapOpen())
                {
                    if (MapSettings.minimap)
                    {
                        if (Zoom() > 0)
                            ToggleMapType(MapMode.Complex);
                        else ToggleMapType(MapMode.Minimap);
                    }
                    else ToggleMapType(MapMode.None);
                }
                else
                {
                    lastMode = mode;
                    CuiHelper.DestroyUi(player, LustyUI.Buttons);
                    ToggleMapType(MapMode.Main);
                }
            }
            public void DisableUser()
            {
                if (!mapOpen) return;
                InvokeHandler.CancelInvoke(this, UpdateMap);
                CuiHelper.DestroyUi(player, LustyUI.Buttons);
                if (mode != MapMode.None)
                    LustyUI.DestroyUI(player);
                mapOpen = false;
            }
            public void EnableUser()
            {
                if (mapOpen) return;                
                ToggleMapType(mode);
            }
            public void EnterEvent() => inEvent = true;
            public void ExitEvent() => inEvent = false;

            #region Friends
            public bool HasFriendList(string name) => friends.ContainsKey(name);
            public void AddFriendList(string name, List<string> friendlist) { friends.Add(name, friendlist); UpdateMembers(); }
            public void RemoveFriendList(string name) { friends.Remove(name); UpdateMembers(); }
            public void UpdateFriendList(string name, List<string> friendlist) { friends[name] = friendlist; UpdateMembers(); }

            public bool HasFriend(string name, string friendId) => friends[name].Contains(friendId);
            public void AddFriend(string name, string friendId) { friends[name].Add(friendId); UpdateMembers(); }
            public void RemoveFriend(string name, string friendId) { friends[name].Remove(friendId); UpdateMembers(); }
            #endregion
            #endregion
        }

        MapUser GetUser(BasePlayer player) => player.GetComponent<MapUser>() ?? null;
        MapUser GetUserByID(string playerId) => mapUsers.ContainsKey(playerId) ? mapUsers[playerId] : null;
        #endregion

        #region Markers
        class ActiveEntity : MonoBehaviour
        {
            public BaseEntity entity;
            private MapMarker marker;
            public AEType type;
            private string icon;

            void Awake()
            {                
                entity = GetComponent<BaseEntity>();
                marker = new MapMarker();
                enabled = false;             
            }
            void OnDestroy()
            {
                InvokeHandler.CancelInvoke(this, UpdatePosition);
            }
            public void SetType(AEType type)
            {                                
                this.type = type;
                switch (type)
                {

                    case AEType.None:
                        break;
                    case AEType.Plane:
                        icon = "plane";
                        marker.name = instance.msg("Plane");
                        break;
                    case AEType.SupplyDrop:
                        icon = "supply";
                        marker.name = instance.msg("Supply Drop");
                        break;
                    case AEType.Helicopter:
                        icon = "heli";
                        marker.name = instance.msg("Helicopter");
                        break;
                    case AEType.Debris:
                        icon = "debris";
                        marker.name = instance.msg("Debris");
                        break;
                    case AEType.Vending:
                        icon = "vending";
                        marker.name = instance.msg("Vending");
                        break;
                    case AEType.Tank:
                        icon = "tank";
                        marker.name = instance.msg("Tank");
                        break;
                    case AEType.Car:
                        icon = "car";
                        marker.name = instance.msg("Car");
                        break;
                }            
                InvokeHandler.InvokeRepeating(this, UpdatePosition, 0.1f, 1f);
            }
            public MapMarker GetMarker() => marker;
            void UpdatePosition()
            {
                if (type == AEType.Helicopter || type == AEType.Plane || type == AEType.Car || type == AEType.Tank)
                {                    
                    marker.r = GetDirection(entity?.transform?.rotation.eulerAngles.y ?? 0);
                    marker.icon = $"{icon}{marker.r}";
                }
                else marker.icon = $"{icon}";
                marker.x = GetPosition(entity.transform.position.x);
                marker.z = GetPosition(entity.transform.position.z);
                if (type == AEType.Vending || type == AEType.SupplyDrop || type == AEType.Debris)
                    InvokeHandler.CancelInvoke(this, UpdatePosition);                
            }
        }
        class MapMarker
        {
            public string name { get; set; }
            public float x { get; set; }
            public float z { get; set; }
            public float r { get; set; }
            public string icon { get; set; }
        }
        static class MapSettings
        {
            static public bool minimap, complexmap, monuments, names, compass, caves, plane, heli, supply, debris, player, allplayers, friends, vending, forcedzoom, cars, tanks;
            static public int zoomlevel;       
        }
        public enum MapMode
        {
            None,
            Main,
            Complex,
            Minimap
        }
        enum AEType
        {
            None,
            Plane,
            SupplyDrop,
            Helicopter,
            Debris,
            Vending,
            Car,
            Tank            
        }
        #endregion

        #region UI
        class LMUI
        {
            static public CuiElementContainer CreateElementContainer(string panelName, string color, string aMin, string aMax, bool cursor = false, string parent = "Hud")
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
                    new CuiElement().Parent = parent,
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
                    Text = { Color = color, FontSize = size, Align = align, FadeIn = 0, Text = text },
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax }
                },
                panel, CuiHelper.GetGuid());

            }
            static public void CreateButton(ref CuiElementContainer container, string panel, string color, string text, int size, string aMin, string aMax, string command, TextAnchor align = TextAnchor.MiddleCenter)
            {                
                container.Add(new CuiButton
                {
                    Button = { Color = color, Command = command, FadeIn = 0 },
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax },
                    Text = { Text = text, FontSize = size, Align = align }
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
                        new CuiRawImageComponent {Png = png, Sprite = "assets/content/textures/generic/fulltransparent.tga" },
                        new CuiRectTransformComponent {AnchorMin = aMin, AnchorMax = aMax }
                    }
                });
            }            
        }        
        #endregion

        #region Oxide Hooks
        void OnNewSave(string filename)
        {
            isNewSave = true;
        }
        void Loaded()
        {
            markerData = Interface.Oxide.DataFileSystem.GetFile($"LustyMap{Path.DirectorySeparatorChar}CustomData");

            mapUsers = new Dictionary<string, MapUser>();
            staticMarkers = new List<MapMarker>();
            customMarkers = new Dictionary<string, MapMarker>();
            temporaryMarkers = new Dictionary<string, MapMarker>();
            entityMarkers = new Dictionary<uint, ActiveEntity>();
            clanData = new Dictionary<string, List<string>>();

            lang.RegisterMessages(Messages, this);
            permission.RegisterPermission("lustymap.admin", this);
        }
        void OnServerInitialized()
        {
            instance = this;

            worldSize = ConVar.Server.worldsize.ToString();
            mapSeed = ConVar.Server.seed.ToString();
            level = ConVar.Server.level;
            mapSize = TerrainMeta.Size.x;
                       
            mapSplitter = new MapSplitter();

            LoadVariables();
            LoadData();            
            LoadSettings();
           
            FindStaticMarkers();
            FindVendingMachines();
            FindVehicles();
            ValidateImages();

            CheckFriends();
            GetClans();
        }
        void OnPlayerInit(BasePlayer player)
        {
            if (player == null) return;
            if (player.HasPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot) || player.IsSleeping())
            {
                timer.In(2, () => OnPlayerInit(player));
                return;
            }
            if (activated)
            {                
                var user = GetUser(player);
                if (user != null)
                {
                    UnityEngine.Object.DestroyImmediate(user);
                    if (mapUsers.ContainsKey(player.UserIDString))
                        mapUsers.Remove(player.UserIDString);
                }

                var mapUser = player.gameObject.AddComponent<MapUser>();
                if (!mapUsers.ContainsKey(player.UserIDString))
                    mapUsers.Add(player.UserIDString, mapUser);
                mapUser.InitializeComponent();
            }
        }
        void OnPlayerDisconnected(BasePlayer player)
        {
            if (player == null) return;            
            if (mapUsers.ContainsKey(player.UserIDString))
            {
                UnityEngine.Object.Destroy(mapUsers[player.UserIDString]);
                mapUsers.Remove(player.UserIDString);
            }

            LustyUI.DestroyUI(player);
        }
        void OnEntitySpawned(BaseEntity entity)
        {
            if (!activated) return;
            if (entity == null) return;
            if (entity is CargoPlane || entity is SupplyDrop || entity is BaseHelicopter || entity is HelicopterDebris || entity is VendingMachine || entity is BaseCar || entity is BradleyAPC)
                AddTemporaryEntityMarker(entity);
        }
        void OnEntityKill(BaseNetworkable entity)
        {
            var activeEntity = entity?.GetComponent<ActiveEntity>();
            if (activeEntity == null) return;
            if (entity?.net?.ID == null) return;
            if (entityMarkers.ContainsKey(entity.net.ID))
                entityMarkers.Remove(entity.net.ID);
            UnityEngine.Object.Destroy(activeEntity);
        }
        void Unload()
        {            
            foreach (var player in BasePlayer.activePlayerList)                            
                OnPlayerDisconnected(player);            

            var mapUsers = UnityEngine.Object.FindObjectsOfType<MapUser>();
            if (mapUsers != null)
                foreach (var user in mapUsers)
                    UnityEngine.Object.DestroyImmediate(user);

            var tempMarkers = UnityEngine.Object.FindObjectsOfType<ActiveEntity>();
            if (tempMarkers != null)
                foreach (var marker in tempMarkers)
                    UnityEngine.Object.DestroyImmediate(marker);
        }
        #endregion

        #region Static UI Generation
        static class LustyUI
        {
            public static string Main = "LMUI_MapMain";
            public static string Mini = "LMUI_MapMini";
            public static string Complex = "LMUI_Complex";
            public static string MainOverlay = "LMUI_MainOverlay";
            public static string MiniOverlay = "LMUI_MiniOverlay";
            public static string ComplexOverlay = "LMUI_ComplexOverlay";
            public static string Buttons = "LMUI_Buttons";

            public static string MainMin;
            public static string MainMax;
            public static string MiniMin;
            public static string MiniMax;

            public static CuiElementContainer StaticMain;
            public static CuiElementContainer StaticMini;
            public static Dictionary<int, CuiElementContainer[,]> StaticComplex = new Dictionary<int, CuiElementContainer[,]>();

            private static Dictionary<ulong, List<string>> OpenUI = new Dictionary<ulong, List<string>>();

            public static void RenameComponents()
            {
                if (StaticMain != null)
                {
                    foreach (var element in StaticMain)
                    {
                        if (element.Name == "AddUI CreatedPanel")
                            element.Name = CuiHelper.GetGuid();
                    }
                }
                if (StaticMini != null)
                {
                    foreach (var element in StaticMini)
                    {
                        if (element.Name == "AddUI CreatedPanel")
                            element.Name = CuiHelper.GetGuid();
                    }
                }
                if (StaticComplex != null)
                {
                    foreach (var size in StaticComplex)
                    {
                        foreach (var piece in size.Value)
                        {
                            foreach (var element in piece)
                            {
                                if (element.Name == "AddUI CreatedPanel")
                                    element.Name = CuiHelper.GetGuid();
                            }
                        }
                    }
                }
                instance.activated = true;
                if (instance.configData.Map.StartOpen)
                    instance.ActivateMaps();
            }
            public static void AddBaseUI(BasePlayer player, MapMode type)
            {
                try {
                    var user = instance.GetUser(player);
                    if (user == null) return;

                    DestroyUI(player);
                    CuiElementContainer element = null;
                    switch (type)
                    {
                        case MapMode.None:
                            return;
                        case MapMode.Main:
                            element = StaticMain;
                            CuiHelper.AddUi(player, StaticMain);
                            AddElementIds(player, ref element);
                            return;
                        case MapMode.Complex:
                            element = StaticComplex[(MapSettings.forcedzoom ? MapSettings.zoomlevel : user.Zoom())][user.Current(true), user.Current(false)];
                            instance.AddMapButtons(player);
                            CuiHelper.AddUi(player, element);
                            AddElementIds(player, ref element);
                            return;
                        case MapMode.Minimap:
                            element = StaticMini;
                            instance.AddMapButtons(player);
                            CuiHelper.AddUi(player, element);
                            AddElementIds(player, ref element);
                            return;
                    }
                }
                catch
                {  
                }
            }
            private static void AddElementIds(BasePlayer player, ref CuiElementContainer container)
            {
                if (!OpenUI.ContainsKey(player.userID))
                    OpenUI.Add(player.userID, new List<string>());
                foreach (var piece in container)
                    OpenUI[player.userID].Add(piece.Name);               
            }
            public static void DestroyUI(BasePlayer player)
            {
                CuiHelper.DestroyUi(player, Buttons);
                CuiHelper.DestroyUi(player, Main);
                CuiHelper.DestroyUi(player, MainOverlay);
                CuiHelper.DestroyUi(player, Mini);
                CuiHelper.DestroyUi(player, MiniOverlay);
                CuiHelper.DestroyUi(player, Complex);
                CuiHelper.DestroyUi(player, ComplexOverlay);
                if (!OpenUI.ContainsKey(player.userID)) return;
                foreach (var piece in OpenUI[player.userID])
                    CuiHelper.DestroyUi(player, piece);
            }         
            public static string Color(string hexColor, float alpha)
            {
                int red = int.Parse(hexColor.Substring(0, 2), NumberStyles.AllowHexSpecifier);
                int green = int.Parse(hexColor.Substring(2, 2), NumberStyles.AllowHexSpecifier);
                int blue = int.Parse(hexColor.Substring(4, 2), NumberStyles.AllowHexSpecifier);
                return $"{(double)red / 255} {(double)green / 255} {(double)blue / 255} {alpha}";
            }
        }

        void GenerateMaps(bool main, bool mini, bool complex)
        {
            if (!ImageLibrary.IsReady())
            {
                timer.In(30, () => GenerateMaps(main, mini, complex));
                Puts("[Warning] Waiting for Image Library to finish processing images");
                return;
            }
            if (main) CreateStaticMain();
            SetMinimapSize();
            if (mini) CreateStaticMini();
            if (complex) CreateStaticComplex();
        }
        void SetMinimapSize()
        {
            float startx = 0f + configData.MiniMap.OffsetSide;
            float endx = startx + (0.13f * configData.MiniMap.HorizontalScale);
            float endy = 1f - configData.MiniMap.OffsetTop;
            float starty = endy - (0.2301f * configData.MiniMap.VerticalScale);
            if (!configData.MiniMap.OnLeftSide)
            {
                endx = 1 - configData.MiniMap.OffsetSide;
                startx = endx - (0.13f * configData.MiniMap.HorizontalScale);
            }
            LustyUI.MiniMin = $"{startx} {starty}";
            LustyUI.MiniMax = $"{endx} {endy}";
        }
        void CreateStaticMain()
        {
            Puts("[Warning] Generating the main map");
            string mapimage = string.Empty;
            if (ImageLibrary.HasImage("mapimage", 0))
                mapimage = GetImage("mapimage");
            else if (ImageLibrary.HasImage("mapimage_high", 0))
                mapimage = GetImage("mapimage_high");
            if (string.IsNullOrEmpty(mapimage))
            {
                Puts("[Error] Unable to load the map image from file storage. This may be caused by slow processing of the images being uploaded to your server. Wait for 5 minutes and reload the plugin. \nIf this problem persists after multiple attempts then unload the plugin and delete your ImageData.json data file or run the 'resetmap' command");
                activated = false;
                return;
            }
            float iconsize = 0.01f;
            LustyUI.MainMin = "0.2271875 0.015";
            LustyUI.MainMax = "0.7728125 0.985";

            var mapContainer = LMUI.CreateElementContainer(LustyUI.Main, "0 0 0 1", LustyUI.MainMin, LustyUI.MainMax, true);
            LMUI.LoadImage(ref mapContainer, LustyUI.Main, mapimage, "0 0", "1 1");
            LMUI.CreatePanel(ref mapContainer, LustyUI.Main, LustyUI.Color("2b627a", 0.4f), "0 0.96", "1 1");
            LMUI.CreateLabel(ref mapContainer, LustyUI.Main, "", $"{Title}  v{Version}", 14, "0.01 0.96", "0.99 1");            

            foreach(var marker in staticMarkers)
            {
                var image = GetImage(marker.icon);
                if (string.IsNullOrEmpty(image)) continue;
                LMUI.LoadImage(ref mapContainer, LustyUI.Main, image, $"{marker.x - iconsize} {marker.z - iconsize}", $"{marker.x + iconsize} {marker.z + iconsize}");
                if (MapSettings.names)
                    LMUI.CreateLabel(ref mapContainer, LustyUI.Main, "", marker.name, 10, $"{marker.x - 0.1} {marker.z - iconsize - 0.03}", $"{marker.x + 0.1} {marker.z - iconsize}");
            }
            LustyUI.StaticMain = mapContainer;
            Puts("[Warning] Main map generated successfully!");
            if (!MapSettings.minimap)            
                LustyUI.RenameComponents();
        }
        void CreateStaticMini()
        {
            Puts("[Warning] Generating the mini-map");
            var mapimage = GetImage("mapimage");
            if (string.IsNullOrEmpty(mapimage))
            {
                Puts("[Error] Unable to load the map image from file storage. This may be caused by slow processing of the images being uploaded to your server. Wait for 5 minutes and reload the plugin. \nIf this problem persists after multiple attempts then unload the plugin and delete your ImageData.json data file or run the 'resetmap' command");
                activated = false;
                return;
            }
            float iconsize = 0.03f;
            
            var mapContainer = LMUI.CreateElementContainer(LustyUI.Mini, "0 0 0 1", LustyUI.MiniMin, LustyUI.MiniMax);
            LMUI.LoadImage(ref mapContainer, LustyUI.Mini, mapimage, "0 0", "1 1");

            foreach (var marker in staticMarkers)
            {
                var image = GetImage(marker.icon);
                if (string.IsNullOrEmpty(image)) continue;
                LMUI.LoadImage(ref mapContainer, LustyUI.Mini, image, $"{marker.x - iconsize} {marker.z - iconsize}", $"{marker.x + iconsize} {marker.z + iconsize}");                
            }
            LustyUI.StaticMini = mapContainer;
            Puts("[Warning] Mini map generated successfully!");
            if (!MapSettings.complexmap)            
                LustyUI.RenameComponents();            
        }       
        void CreateStaticComplex()
        {
            Puts("[Warning] Generating the complex map. This may take a few moments, please wait!");            
            foreach (var mapslices in new List<int> { 6, 12, 26 })//, 32 })
            {
                for (int number = 0; number < (mapslices * mapslices); number++)
                {
                    int rowNum = 0;
                    int colNum = 0;
                    if (number > mapslices - 1)
                    {
                        colNum = Convert.ToInt32(Math.Floor((float)number / (float)mapslices));
                        rowNum = number - (colNum * mapslices);
                    }
                    else rowNum = number;
                    
                    var mapContainer = LMUI.CreateElementContainer(LustyUI.Complex, "0 0 0 1", LustyUI.MiniMin, LustyUI.MiniMax);

                    string imageId = GetImage($"map-{mapslices}-{rowNum}-{colNum}");
                    if (!string.IsNullOrEmpty(imageId))
                        LMUI.LoadImage(ref mapContainer, LustyUI.Complex, imageId, $"0 0", $"1 1");
                    else
                    {
                        PrintError($"Missing map piece (Slices: {mapslices}, Column: {colNum}, Row: {rowNum}). When the plugin is splitting the map you must wait for it to finish otherwise the split will not complete and this error will occur");
                        PrintError($"Creating a new load order with ImageLibrary");
                        LoadImages();
                        LoadMapImage();
                        return;
                    }

                    double width = ((double)1 / (double)mapslices);
                    float iconsize = 0.03f;

                    var column = colNum;
                    var row = rowNum;
                    if (column < 1) column = 1;
                    if (column > mapslices - 2) column = mapslices - 2;
                    if (row < 1) row = 1;
                    if (row > mapslices - 2) row = mapslices - 2;                    

                    double colStart = (width * column) - width;
                    double colEnd = colStart + (width * 3);

                    double rowStart = 1 - ((width * row) - width);
                    double rowEnd = (rowStart - (width * 3));                   

                    foreach (var marker in staticMarkers)
                    {
                        string markerId = GetImage(marker.icon);
                        if (string.IsNullOrEmpty(markerId)) continue;

                        float x = marker.x;
                        float z = marker.z;
                        if ((x > colStart && x < colEnd) && (z > rowEnd && z < rowStart))
                        {
                            var average = 1 / (colEnd - colStart);
                            double posX = (x - colStart) * average;
                            double posZ = (z - rowEnd) * average;
                            LMUI.LoadImage(ref mapContainer, LustyUI.Complex, markerId, $"{posX - iconsize} {posZ - iconsize}", $"{posX + iconsize} {posZ + iconsize}");
                        }
                    }
                    int zoom = CountToZoom(mapslices);

                    if (!LustyUI.StaticComplex.ContainsKey(zoom))
                        LustyUI.StaticComplex.Add(zoom, new CuiElementContainer[mapslices, mapslices]);
                    LustyUI.StaticComplex[zoom][colNum, rowNum] = mapContainer;
                }
            }
            Puts("[Warning] Complex map generated successfully!");
            LustyUI.RenameComponents();            
        }
        
        static int ZoomToCount(int zoom)
        {
            switch (zoom)
            {
                case 1:
                    return 6;
                case 2:
                    return 12;
                case 3:
                    return 26;
                case 4:
                    return 32;
                default:
                    return 0;
            }
        }
        static int CountToZoom(int count)
        {
            switch (count)
            {
                case 6:
                    return 1;
                case 12:
                    return 2;
                case 26:
                    return 3;
                case 32:
                    return 4;
                default:
                    return 0;
            }
        }
        #endregion

        #region Maps
        void ActivateMaps()
        {
            foreach (var player in BasePlayer.activePlayerList)            
                OnPlayerInit(player);            
        }
        void AddMapCompass(BasePlayer player, ref CuiElementContainer mapContainer, string panel, int fontsize, string offsetMin, string offsetMax)
        {
            string direction = null;
            if (player?.eyes?.rotation == null) return;
            float lookRotation = player?.eyes?.rotation.eulerAngles.y ?? 0;
            int playerdirection = (Convert.ToInt16((lookRotation - 5) / 10 + 0.5) * 10);
            if (lookRotation >= 355) playerdirection = 0;
            if (lookRotation > 337.5 || lookRotation < 22.5) { direction = msg("cpsN"); }
            else if (lookRotation > 22.5 && lookRotation < 67.5) { direction = msg("cpsNE"); }
            else if (lookRotation > 67.5 && lookRotation < 112.5) { direction = msg("cpsE"); }
            else if (lookRotation > 112.5 && lookRotation < 157.5) { direction = msg("cpsSE"); }
            else if (lookRotation > 157.5 && lookRotation < 202.5) { direction = msg("cpsS"); }
            else if (lookRotation > 202.5 && lookRotation < 247.5) { direction = msg("cpsSW"); }
            else if (lookRotation > 247.5 && lookRotation < 292.5) { direction = msg("cpsW"); }
            else if (lookRotation > 292.5 && lookRotation < 337.5) { direction = msg("cpsNW"); }
            LMUI.CreateLabel(ref mapContainer, panel, "", $"<size={fontsize + 4}>{direction}</size> \n{player.transform.position}", fontsize, offsetMin, offsetMax, TextAnchor.UpperCenter);
        }
        void AddMapButtons(BasePlayer player)
        {
            float startx = 0f + configData.MiniMap.OffsetSide;
            float endx = startx + (0.13f * configData.MiniMap.HorizontalScale);
            float endy = 1f - configData.MiniMap.OffsetTop;
            float starty = endy - (0.2301f * configData.MiniMap.VerticalScale);
            string b_text = "<<<";
            var container = LMUI.CreateElementContainer(LustyUI.Buttons, "0 0 0 0", $"{endx + 0.001f} {starty}", $"{endx + 0.02f} {endy}");

            if (!configData.MiniMap.OnLeftSide)
            {
                endx = 1 - configData.MiniMap.OffsetSide;
                startx = endx - (0.13f * configData.MiniMap.HorizontalScale);
                b_text = ">>>";
                container = LMUI.CreateElementContainer(LustyUI.Buttons, "0 0 0 0", $"{startx - 0.02f} {starty}", $"{startx - 0.001f} {endy}");
            }
           
            LMUI.CreateButton(ref container, LustyUI.Buttons, LustyUI.Color("696969", 0.6f), b_text, 12, $"0 0.9", $"1 1", "LMUI_Control shrink");
            if (MapSettings.complexmap && !MapSettings.forcedzoom)
            {
                LMUI.CreateButton(ref container, LustyUI.Buttons, LustyUI.Color("696969", 0.6f), "+", 14, $"0 0.79", $"1 0.89", "LMUI_Control zoomin");
                LMUI.CreateButton(ref container, LustyUI.Buttons, LustyUI.Color("696969", 0.6f), "-", 14, $"0 0.68", $"1 0.78", "LMUI_Control zoomout");
            }
            CuiHelper.DestroyUi(player, LustyUI.Buttons);
            CuiHelper.AddUi(player, container);
        }
        void CreateShrunkUI(BasePlayer player)
        {
            var user = GetUser(player);
            if (user == null) return;

            float b_endy = 0.999f - configData.MiniMap.OffsetTop;
            float b_startx = 0.001f + configData.MiniMap.OffsetSide;
            float b_endx = b_startx + 0.02f;
            string b_text = ">>>";

            if (!configData.MiniMap.OnLeftSide)
            {                
                b_endx = 0.999f - configData.MiniMap.OffsetSide;
                b_startx = b_endx - 0.02f;
                b_text = "<<<";
            }                       
            var container = LMUI.CreateElementContainer(LustyUI.Buttons, "0 0 0 0", $"{b_startx} {b_endy - 0.025f}", $"{b_endx} {b_endy}");
            LMUI.CreateButton(ref container, LustyUI.Buttons, LustyUI.Color("696969", 0.6f), b_text, 12, "0 0", "1 1", "LMUI_Control expand");
            CuiHelper.DestroyUi(player, LustyUI.Buttons);
            CuiHelper.AddUi(player, container);
        }

        #region Standard Maps
        void OpenMainMap(BasePlayer player) => LustyUI.AddBaseUI(player, MapMode.Main);
        void OpenMiniMap(BasePlayer player) => LustyUI.AddBaseUI(player, MapMode.Minimap);
        void UpdateOverlay(BasePlayer player, string panel, string posMin, string posMax, float iconsize)
        {
            var mapContainer = LMUI.CreateElementContainer(panel, "0 0 0 0", posMin, posMax);

            var user = GetUser(player);
            if (user == null) return;            
            foreach (var marker in customMarkers)
            {
                var image = GetImage(marker.Key);
                if (string.IsNullOrEmpty(image)) continue;
                AddIconToMap(ref mapContainer, panel, image, marker.Value.name, iconsize * 1.25f, marker.Value.x, marker.Value.z);
            }
            foreach (var marker in temporaryMarkers)
            {
                var image = GetImage(marker.Key);
                if (string.IsNullOrEmpty(image)) continue;
                AddIconToMap(ref mapContainer, panel, image, marker.Value.name, iconsize * 1.25f, marker.Value.x, marker.Value.z);
            }
            foreach (var entity in entityMarkers)
            {
                if (entity.Value.type == AEType.Car && (entity.Value.entity as BaseCar).IsMounted())
                    continue;
                var marker = entity.Value.GetMarker();
                if (marker == null) continue;
                var image = GetImage(marker.icon);
                if (string.IsNullOrEmpty(image)) continue;                
                AddIconToMap(ref mapContainer, panel, image, "", entity.Value.type == AEType.Vending ? iconsize : iconsize * 1.4f, marker.x, marker.z);
            }            
            if (user.IsAdmin || MapSettings.allplayers)
            {
                foreach (var mapuser in mapUsers)
                {
                    if (mapuser.Key == player.UserIDString) continue;

                    var marker = mapuser.Value.GetMarker();
                    if (marker == null) continue;
                    var image = GetImage($"other{marker.r}");
                    if (string.IsNullOrEmpty(image)) continue;
                    AddIconToMap(ref mapContainer, panel, image, marker.name, iconsize * 1.25f, marker.x, marker.z);                    
                }
            }
            else if (MapSettings.friends)
            {
                foreach (var friendId in user.friendList)
                {
                    if (friendId == player.UserIDString) continue;

                    if (mapUsers.ContainsKey(friendId))
                    {
                        var friend = mapUsers[friendId];
                        if (friend.InEvent() && configData.Map.HideEventPlayers) continue;
                        var marker = friend.GetMarker();
                        if (marker == null) continue;
                        var image = GetImage($"friend{marker.r}");
                        if (string.IsNullOrEmpty(image)) continue;
                        AddIconToMap(ref mapContainer, panel, image, marker.name, iconsize * 1.25f, marker.x, marker.z);
                    }
                }
            }
            if (MapSettings.player)
            {
                var selfMarker = user.GetMarker();
                if (selfMarker != null)
                {
                    var selfImage = GetImage($"self{selfMarker.r}");
                    AddIconToMap(ref mapContainer, panel, selfImage, "", iconsize * 1.25f, selfMarker.x, selfMarker.z);
                }
            }

            if (panel == LustyUI.MainOverlay)
            {                
                LMUI.CreateButton(ref mapContainer, panel, LustyUI.Color("88a8b6", 1), "X", 14, "0.95 0.961", "0.999 0.999", "LMUI_Control map");
                if (MapSettings.compass)
                    AddMapCompass(player, ref mapContainer, panel, 14, "0.75 0.88", "1 0.95");
            }

            if (panel == LustyUI.MiniOverlay)
            {                
                if (MapSettings.compass)
                    AddMapCompass(player, ref mapContainer, panel, 10, "0 -0.25", "1 -0.02");                
            }

            CuiHelper.DestroyUi(player, panel);
            CuiHelper.AddUi(player, mapContainer);
        }
        void AddIconToMap(ref CuiElementContainer mapContainer, string panel, string image, string name, float iconsize, float posX, float posZ)
        {
            if (posX < iconsize || posX > 1 - iconsize || posZ < iconsize || posZ > 1 - iconsize) return;
            LMUI.LoadImage(ref mapContainer, panel, image, $"{posX - iconsize} {posZ - iconsize}", $"{posX + iconsize} {posZ + iconsize}");
            if (MapSettings.names)
                LMUI.CreateLabel(ref mapContainer, panel, "", name, 10, $"{posX - 0.1} {posZ - iconsize - 0.025}", $"{posX + 0.1} {posZ - iconsize}");
        }        
        #endregion

        #region Complex Maps
        void OpenComplexMap(BasePlayer player) => LustyUI.AddBaseUI(player, MapMode.Complex);
        void UpdateCompOverlay(BasePlayer player)
        {
            var mapContainer = LMUI.CreateElementContainer(LustyUI.ComplexOverlay, "0 0 0 0", LustyUI.MiniMin, LustyUI.MiniMax);

            var user = GetUser(player);
            if (user == null) return;

            var colNum = user.Current(true);
            var rowNum = user.Current(false);

            var mapslices = ZoomToCount(user.Zoom());
            double width = ((double)1 / (double)mapslices);
            float iconsize = 0.04f;

            var column = colNum;
            var row = rowNum;
            if (column < 1) column = 1;
            if (column > mapslices - 2) column = mapslices - 2;
            if (row < 1) row = 1;
            if (row > mapslices - 2) row = mapslices - 2;

            double colStart = (width * column) - width;
            double colEnd = colStart + (width * 3);

            double rowStart = 1 - ((width * row) - width);
            double rowEnd = (rowStart - (width * 3));

            foreach (var marker in customMarkers)
            {
                var image = GetImage(marker.Key);
                if (string.IsNullOrEmpty(image)) continue;
                AddComplexIcon(ref mapContainer, LustyUI.ComplexOverlay, image, "", iconsize * 1.3f, marker.Value.x, marker.Value.z, colStart, colEnd, rowStart, rowEnd);
            }
            foreach (var marker in temporaryMarkers)
            {
                var image = GetImage(marker.Key);
                if (string.IsNullOrEmpty(image)) continue;
                AddComplexIcon(ref mapContainer, LustyUI.ComplexOverlay, image, "", iconsize * 1.3f, marker.Value.x, marker.Value.z, colStart, colEnd, rowStart, rowEnd);
            }
            foreach (var entity in entityMarkers)
            {
                if (entity.Value.type == AEType.Car && (entity.Value.entity as BaseCar).IsMounted())
                    continue;
                var marker = entity.Value.GetMarker();
                if (marker == null) continue;
                var image = GetImage(marker.icon);
                if (string.IsNullOrEmpty(image)) continue;
                AddComplexIcon(ref mapContainer, LustyUI.ComplexOverlay, image, "", entity.Value.type == AEType.Vending ? iconsize : iconsize * 1.6f, marker.x, marker.z, colStart, colEnd, rowStart, rowEnd);
            }
            if (user.IsAdmin || MapSettings.allplayers)
            {
                foreach (var mapuser in mapUsers)
                {
                    if (mapuser.Key == player.UserIDString) continue;

                    var marker = mapuser.Value.GetMarker();
                    if (marker == null) continue;
                    var image = GetImage($"other{marker.r}");
                    if (string.IsNullOrEmpty(image)) continue;
                    AddComplexIcon(ref mapContainer, LustyUI.ComplexOverlay, image, "", iconsize * 1.3f, marker.x, marker.z, colStart, colEnd, rowStart, rowEnd);
                }
            }
            else if (MapSettings.friends)
            {
                foreach (var friendId in user.friendList)
                {
                    if (friendId == player.UserIDString) continue;

                    if (mapUsers.ContainsKey(friendId))
                    {
                        var friend = mapUsers[friendId];
                        if (friend.InEvent() && configData.Map.HideEventPlayers) continue;
                        var marker = friend.GetMarker();
                        if (marker == null) continue;
                        var image = GetImage($"friend{marker.r}");
                        if (string.IsNullOrEmpty(image)) continue;
                        AddComplexIcon(ref mapContainer, LustyUI.ComplexOverlay, image, "", iconsize * 1.3f, marker.x, marker.z, colStart, colEnd, rowStart, rowEnd);
                    }
                }
            }            
            if (MapSettings.player)
            {
                var selfMarker = user.GetMarker();
                if (selfMarker != null)
                {
                    var selfImage = GetImage($"self{selfMarker.r}");
                    if (!string.IsNullOrEmpty(selfImage))
                        AddComplexIcon(ref mapContainer, LustyUI.ComplexOverlay, selfImage, "", iconsize * 1.25f, selfMarker.x, selfMarker.z, colStart, colEnd, rowStart, rowEnd);
                }
            }
            if (MapSettings.compass)
                AddMapCompass(player, ref mapContainer, LustyUI.ComplexOverlay, 10, "0 -0.25", "1 -0.02");
            
            CuiHelper.DestroyUi(player, LustyUI.ComplexOverlay);
            CuiHelper.AddUi(player, mapContainer);
        }
        void AddComplexIcon(ref CuiElementContainer mapContainer, string panel, string image, string name, float iconsize, float x, float z, double colStart, double colEnd, double rowStart, double rowEnd)
        {
            if ((x > colStart && x < colEnd) && (z > rowEnd && z < rowStart))
            {
                var average = 1 / (colEnd - colStart);
                double posX = (x - colStart) * average;
                double posZ = (z - rowEnd) * average;

                if (posX < 0 + iconsize || posX > 1 - iconsize || posZ < 0 + iconsize || posZ > 1 - iconsize) return;
                LMUI.LoadImage(ref mapContainer, panel, image, $"{posX - iconsize} {posZ - iconsize}", $"{posX + iconsize} {posZ + iconsize}");
                if (MapSettings.names)
                    LMUI.CreateLabel(ref mapContainer, panel, "", name, 10, $"{posX - 0.1} {posZ - iconsize - 0.025}", $"{posX + 0.1} {posZ - iconsize}");
            }
        }
        #endregion
        #endregion

        #region Commands
        [ConsoleCommand("LMUI_Control")]
        private void cmdLustyControl(ConsoleSystem.Arg arg)
        {
            if (!activated) return;
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            var user = GetUser(player);
            if (user == null) return;
            switch (arg.Args[0].ToLower())
            {
                case "map":                              
                    user.ToggleMain();
                    return;               
                case "shrink":
                    user.ToggleMapType(MapMode.None);
                    return;
                case "expand":
                    if (user.Zoom() > 0)
                        user.ToggleMapType(MapMode.Complex);
                    else user.ToggleMapType(MapMode.Minimap);
                    return;
                case "zoomin":
                    user.Zoom(true);
                    break;
                case "zoomout":
                    user.Zoom(false);
                    return;
                default:
                    return;
            }
        }
        [ConsoleCommand("resetmap")]
        void ccmdResetmap(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null) return;
            SendReply(arg, "Map reset Confirmed! Creating a new image load order with ImageLibrary");
            LoadImages();
            LoadMapImage();                 
        }
        [ChatCommand("map")]
        void cmdOpenMap(BasePlayer player, string command, string[] args)
        {
            if (!activated)
            {
                SendReply(player, "LustyMap is not activated");
                return;
            }
            var user = GetUser(player);
            if (user == null)
            {
                user = player.gameObject.AddComponent<MapUser>();
                mapUsers.Add(player.UserIDString, user);
                user.InitializeComponent();
            }
            if (args.Length == 0)
                user.ToggleMapType(MapMode.Main);
            else
            {
                if (args[0].ToLower() == "mini")
                    user.ToggleMapType(MapMode.Minimap);
                if (args[0].ToLower() == "admin")
                {
                    if (!player.IsAdmin && !permission.UserHasPermission(player.UserIDString, "lustymap.admin")) return;
                    if (user.IsAdmin)
                    {
                        user.ToggleAdmin(false);
                        SendReply(player, "Admin mode disabled");
                    }
                    else
                    {
                        user.ToggleAdmin(true);
                        SendReply(player, "Admin mode enabled");
                    }
                }
            }
        }

        [ChatCommand("marker")]
        void cmdMarker(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin && !permission.UserHasPermission(player.UserIDString, "lustymap.admin")) return;
            if (args.Length == 0)
            {
                SendReply(player, "Add and remove markers / custom icons to the map. See the overview for information regarding using custom icons");
                SendReply(player, "/marker add <name> <opt:iconname> - Adds a new markers with the name specified at your location.");
                SendReply(player, "/marker remove <name> - Removes the marker with the name specified");
                return;
            }
            if (args.Length < 2)
            {
                SendReply(player, "You must enter a marker name!");
                return;
            }
            var name = args[1];
            switch (args[0].ToLower())
            {
                case "add":
                    string icon = "special";
                    if (args.Length > 2)
                        icon = args[2];
                    if (AddMarker(player.transform.position.x, player.transform.position.z, name, icon))
                        SendReply(player, $"You have successfully added a new map marker with the name: {name}");  
                    else SendReply(player, $"A map marker with the name \"{name}\" already exists");
                    return;
                case "remove":
                    if (RemoveMarker(name))
                        SendReply(player, $"You have successfully removed the map marker with the name: {name}");
                    else SendReply(player, $"No map marker with the name \"{name}\" exists");
                    return;
                default:
                    SendReply(player, "Incorrect syntax used. type \"/marker\" for more information");
                    break;
            }
        }
        #endregion

        #region Functions    
        private void CheckFriends()
        {
            if (Friends)
            {
                if (Friends.ResourceId == 686)               
                    isRustFriends = true;
            }
        }
        private void AddTemporaryEntityMarker(BaseEntity entity)
        {
            if (entity == null) return;
            if (entity?.net?.ID == null) return;
            AEType type = AEType.None;
            if (entity is CargoPlane)
            {
                if (!configData.Markers.ShowPlanes) return;
                type = AEType.Plane;                
            }
            else if (entity is BaseHelicopter)
            {
                if (!configData.Markers.ShowHelicopters) return;
                type = AEType.Helicopter;               
            }
            else if (entity is BradleyAPC)
            {
                if (!configData.Markers.ShowTanks) return;
                type = AEType.Tank;
            }
            else if (entity is BaseCar)
            {
                if (!configData.Markers.ShowCars) return;
                type = AEType.Car;
            }
            else if (entity is SupplyDrop)
            {
                if (!configData.Markers.ShowSupplyDrops) return;
                type = AEType.SupplyDrop;                
            }
            else if (entity is HelicopterDebris)
            {
                if (!configData.Markers.ShowDebris) return;
                type = AEType.Debris;                
            }  
            else if (entity is VendingMachine)
            {
                if (!configData.Markers.ShowPublicVendingMachines) return;
                if (!(entity as VendingMachine).IsBroadcasting()) return;
                type = AEType.Vending;
            }         
            var actEnt = entity.gameObject.AddComponent<ActiveEntity>();
            actEnt.SetType(type);

            entityMarkers.Add(entity.net.ID, actEnt);
        }
        private void LoadSettings()
        {
            MapSettings.caves = configData.Markers.ShowCaves;
            MapSettings.compass = configData.Map.ShowCompass;
            MapSettings.debris = configData.Markers.ShowDebris;
            MapSettings.heli = configData.Markers.ShowHelicopters;
            MapSettings.monuments = configData.Markers.ShowMonuments;
            MapSettings.plane = configData.Markers.ShowPlanes;
            MapSettings.player = configData.Markers.ShowPlayer;
            MapSettings.allplayers = configData.Markers.ShowAllPlayers;
            MapSettings.supply = configData.Markers.ShowSupplyDrops;
            MapSettings.friends = configData.Markers.ShowFriends;
            MapSettings.names = configData.Markers.ShowMarkerNames;
            MapSettings.minimap = configData.MiniMap.UseMinimap;
            MapSettings.vending = configData.Markers.ShowPublicVendingMachines;
            MapSettings.complexmap = configData.ComplexOptions.UseComplexMap;
            MapSettings.forcedzoom = configData.ComplexOptions.ForceMapZoom;
            MapSettings.zoomlevel = configData.ComplexOptions.ForcedZoomLevel;
            MapSettings.cars = configData.Markers.ShowCars;
            MapSettings.tanks = configData.Markers.ShowTanks;

            if (MapSettings.zoomlevel < 1)
                MapSettings.zoomlevel = 1;
            if (MapSettings.zoomlevel > 3)
                MapSettings.zoomlevel = 3;
        }        
        private void FindStaticMarkers()
        {
            if (MapSettings.monuments)
            { 
                var monuments = UnityEngine.Object.FindObjectsOfType<MonumentInfo>();
                foreach (var monument in monuments)
                {                    
                    MapMarker mon = new MapMarker
                    {
                        x = GetPosition(monument.transform.position.x),
                        z = GetPosition(monument.transform.position.z)
                    };

                    if (monument.name.Contains("lighthouse"))
                    {
                        mon.name = msg("lighthouse");
                        mon.icon = "lighthouse";
                        staticMarkers.Add(mon);
                        continue;
                    }
                    if (monument.Type == MonumentType.Cave && MapSettings.caves)
                    {
                        mon.name = msg("cave");
                        mon.icon = "cave";
                        staticMarkers.Add(mon);
                        continue;
                    }
                    
                    if (monument.name.Contains("powerplant_1"))
                    {
                        mon.name = msg("powerplant");
                        mon.icon = "special";
                        staticMarkers.Add(mon);
                        continue;
                    }
                    if(monument.name.Contains("harbor_1"))
                    {
                        mon.name = msg("small harbor");
                        mon.icon = "harbor";
                        staticMarkers.Add(mon);
                        continue;
                    }
                    if (monument.name.Contains("harbor_2"))
                    {
                        mon.name = msg("big harbor");
                        mon.icon = "harbor";
                        staticMarkers.Add(mon);
                        continue;
                    }
                    if (monument.name.Contains("military_tunnel_1"))
                    {
                        mon.name = msg("militarytunnel");
                        mon.icon = "special";
                        staticMarkers.Add(mon);
                        continue;
                    }

                    if (monument.name.Contains("airfield_1"))
                    {
                        mon.name = msg("airfield");
                        mon.icon = "special";
                        staticMarkers.Add(mon);
                        continue;
                    }

                    if (monument.name.Contains("trainyard_1"))
                    {
                        mon.name = msg("trainyard");
                        mon.icon = "special";
                        staticMarkers.Add(mon);
                        continue;
                    }

                    if (monument.name.Contains("water_treatment_plant_1"))
                    {
                        mon.name = msg("waterplant");
                        mon.icon = "special";
                        staticMarkers.Add(mon);
                        continue;
                    }

                    if (monument.name.Contains("warehouse"))
                    {
                        mon.name = msg("warehouse");
                        mon.icon = "warehouse";
                        staticMarkers.Add(mon);
                        continue;
                    }

                    if (monument.name.Contains("satellite_dish"))
                    {

                        mon.name = msg("dish");
                        mon.icon = "dish";
                        staticMarkers.Add(mon);
                        continue;
                    }

                    if (monument.name.Contains("sphere_tank"))
                    {
                        mon.name = msg("spheretank");
                        mon.icon = "spheretank";
                        staticMarkers.Add(mon);
                        continue;
                    }

                    if (monument.name.Contains("radtown_small_3"))
                    {
                        mon.name = msg("radtown");
                        mon.icon = "radtown";
                        staticMarkers.Add(mon);
                        continue;
                    }

                    if (monument.name.Contains("launch_site_1"))
                    {
                        mon.name = msg("rocketfactory");
                        mon.icon = "rocketfactory";
                        staticMarkers.Add(mon);
                        continue;
                    }

                    if (monument.name.Contains("gas_station_1"))
                    {
                        mon.name = msg("gasstation");
                        mon.icon = "gasstation";
                        staticMarkers.Add(mon);
                        continue;
                    }

                    if (monument.name.Contains("supermarket_1"))
                    {
                        mon.name = msg("supermarket");
                        mon.icon = "supermarket";
                        staticMarkers.Add(mon);
                        continue;
                    }

                    if (monument.name.Contains("mining_quarry_c"))
                    {
                        mon.name = msg("quarryhqm");
                        mon.icon = "quarryhqm";
                        staticMarkers.Add(mon);
                        continue;
                    }

                    if (monument.name.Contains("mining_quarry_a"))
                    {
                        mon.name = msg("quarrysulfur");
                        mon.icon = "quarrysulfur";
                        staticMarkers.Add(mon);
                        continue;
                    }

                    if (monument.name.Contains("mining_quarry_b"))
                    {
                        mon.name = msg("quarrystone");
                        mon.icon = "quarrystone";
                        staticMarkers.Add(mon);
                        continue;
                    }

                    if (monument.name.Contains("junkyard_1"))
                    {
                        mon.name = msg("junkyard");
                        mon.icon = "junkyard";
                        staticMarkers.Add(mon);
                        continue;
                    }
                }
            }                      
        }
        private void FindVendingMachines()
        {
            if (!MapSettings.vending) return;
            var machines = UnityEngine.Object.FindObjectsOfType<VendingMachine>();
            foreach (var vendor in machines)
            {
                AddTemporaryEntityMarker(vendor);
            }
        }
        private void FindVehicles()
        {
            if (MapSettings.cars)
            {
                var cars = UnityEngine.Object.FindObjectsOfType<BaseCar>();
                foreach (var car in cars)
                {
                    AddTemporaryEntityMarker(car);
                }
            }
            
            if (MapSettings.tanks)
            {
                var tanks = UnityEngine.Object.FindObjectsOfType<BradleyAPC>();
                foreach (var tank in tanks)
                {
                    AddTemporaryEntityMarker(tank);
                }
            }
        }
        static double GrabCurrentTime() => DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1, 0, 0, 0)).TotalSeconds;
        #endregion

        #region Helpers
        public static string RemoveSpecialCharacters(string str)
        {
            StringBuilder sb = new StringBuilder();
            foreach (char c in str)
            {
                if ((c >= '0' && c <= '9') || (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= 'А' && c <= 'Я') || (c >= 'а' && c <= 'я') || c == '.' || c == '_')
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }
        static float GetPosition(float pos) => (pos + mapSize / 2f) / mapSize;  
        static int GetDirection(float rotation) => (int)((rotation - 5) / 10 + 0.5) * 10;
        #endregion

        #region API
        void EnableMaps(BasePlayer player)
        {
            var user = GetUser(player);
            if (user != null)
                user.EnableUser();
        }
        void DisableMaps(BasePlayer player)
        {
            var user = GetUser(player);
            if (user != null)
                user.DisableUser();
        }

        #region Markers
        bool AddMarker(float x, float z, string name, string icon = "special", float r = 0)
        {
            if (customMarkers.ContainsKey(name)) return false;
            MapMarker marker = new MapMarker
            {
                icon = icon,
                name = name,
                x = GetPosition(x),
                z = GetPosition(z),
                r = r
            };
            if (r > 0) marker.r = GetDirection(r);
            customMarkers.Add(name, marker);
            if (!string.IsNullOrEmpty(icon) && icon != "special")
            {
                string url = icon;
                if (!url.StartsWith("http") && !url.StartsWith("www") && !url.StartsWith("file://"))
                    url = $"{dataDirectory}custom{Path.DirectorySeparatorChar}{icon}.png";
                ImageLibrary.AddImage(url, name, 0);
            }
            SaveMarkers();
            return true;
        }
        void UpdateMarker(float x, float z, string name, string icon = "special", float r = 0)
        {
            if (!customMarkers.ContainsKey(name)) return;
            MapMarker marker = new MapMarker
            {
                icon = icon,
                name = name,
                x = GetPosition(x),
                z = GetPosition(z),
                r = r
            };
            if (r > 0) marker.r = GetDirection(r);
            customMarkers[name] = marker;

            if (!string.IsNullOrEmpty(icon) && icon != "special" && !ImageLibrary.HasImage(name, 0))
            {
                string url = icon;
                if (!url.StartsWith("http") && !url.StartsWith("www"))
                    url = $"{dataDirectory}custom{Path.DirectorySeparatorChar}{icon}.png";
                ImageLibrary.AddImage(url, name, 0);
            }
            SaveMarkers();
        }
        bool RemoveMarker(string name)
        {
            if (!customMarkers.ContainsKey(name)) return false;
            customMarkers.Remove(name);
            SaveMarkers();
            return true;
        }

        bool AddTemporaryMarker(float x, float z, string name, string icon = "special", float r = 0)
        {
            if (temporaryMarkers.ContainsKey(name)) return false;
            MapMarker marker = new MapMarker
            {
                icon = icon,
                name = name,
                x = GetPosition(x),
                z = GetPosition(z),
                r = r
            };
            if (r > 0) marker.r = GetDirection(r);
            temporaryMarkers.Add(name, marker);
            if (!string.IsNullOrEmpty(icon) && icon != "special")
            {
                string url = icon;
                if (!url.StartsWith("http") && !url.StartsWith("www") && !url.StartsWith("file://"))
                    url = $"{dataDirectory}custom{Path.DirectorySeparatorChar}{icon}.png";
                ImageLibrary.AddImage(url, name, 0);
            }
            return true;
        }
        void UpdateTemporaryMarker(float x, float z, string name, string icon = "special", float r = 0)
        {
            if (!temporaryMarkers.ContainsKey(name)) return;
            MapMarker marker = new MapMarker
            {
                icon = icon,
                name = name,
                x = GetPosition(x),
                z = GetPosition(z),
                r = r
            };
            if (r > 0) marker.r = GetDirection(r);
            temporaryMarkers[name] = marker;

            if (!string.IsNullOrEmpty(icon) && icon != "special" && !ImageLibrary.HasImage(name, 0))
            {
                string url = icon;
                if (!url.StartsWith("http") && !url.StartsWith("www"))
                    url = $"{dataDirectory}custom{Path.DirectorySeparatorChar}{icon}.png";
                ImageLibrary.AddImage(url, name, 0);
            }
        }
        bool RemoveTemporaryMarker(string name)
        {
            if (!temporaryMarkers.ContainsKey(name)) return false;
            temporaryMarkers.Remove(name);
            return true;
        }
        bool RemoveTemporaryMarkerStartsWith(string name)
        {
            var keyArray = temporaryMarkers.Keys.ToArray();
            for (int i = 0; i < temporaryMarkers.Count; i++)
            {
                string key = keyArray[i];
                if (key.StartsWith(name))
                    temporaryMarkers.Remove(key);

            }            
            return true;
        }
        #endregion

        #region Friends
        bool AddFriendList(string playerId, string name, List<string> list, bool bypass = false)
        {
            if (!bypass && !configData.Friends.AllowCustomLists) return false;
            var user = GetUserByID(playerId);
            if (user == null) return false;
            if (user.HasFriendList(name))
                return false;

            user.AddFriendList(name, list);
            return true;
        }
        bool RemoveFriendList(string playerId, string name, bool bypass = false)
        {
            if (!bypass && !configData.Friends.AllowCustomLists) return false;
            var user = GetUserByID(playerId);
            if (user == null) return false;
            if (!user.HasFriendList(name))
                return false;

            user.RemoveFriendList(name);
            return true;
        }
        bool UpdateFriendList(string playerId, string name, List<string> list, bool bypass = false)
        {
            if (!bypass && !configData.Friends.AllowCustomLists) return false;
            var user = GetUserByID(playerId);
            if (user == null) return false;
            if (!user.HasFriendList(name))
                return false;

            user.UpdateFriendList(name, list);
            return true;
        }
        bool AddFriend(string playerId, string name, string friendId, bool bypass = false)
        {
            if (!bypass && !configData.Friends.AllowCustomLists) return false;
            var user = GetUserByID(playerId);
            if (user == null) return false;
            if (!user.HasFriendList(name))
                user.AddFriendList(name, new List<string>());
            if (user.HasFriend(name, friendId))
                return true;
            user.AddFriend(name, friendId);
            return true;
        }
        bool RemoveFriend(string playerId, string name, string friendId, bool bypass = false)
        {
            if (!bypass && !configData.Friends.AllowCustomLists) return false;
            var user = GetUserByID(playerId);
            if (user == null) return false;
            if (!user.HasFriendList(name))
                return false;
            if (!user.HasFriend(name, friendId))
                return true;
            user.RemoveFriend(name, friendId);
            return true;
        }
        #endregion
               
        string GetMap() => GetImage("mapimage"); 
        bool SplitMap(int splices)
        {
            mapSplitter.SplitMap(GetImage("mapimage_high"), splices);
            return true;
        }

        #endregion

        #region External API  
        void JoinedEvent(BasePlayer player)
        {
            var user = GetUser(player);
            if (user != null)
                user.EnterEvent();
        }
        void LeftEvent(BasePlayer player)
        {
            var user = GetUser(player);
            if (user != null)
                user.ExitEvent();
        }

        #region Friends
        List<string> GetFriends(ulong playerId)
        {
            if (Friends != null)
            {
                if (isRustFriends)
                    return GetRustFriends(playerId);
                return GetUniversalFriends(playerId);
            }
            return new List<string>();
        }
        List<string> GetRustFriends(ulong playerId)
        {
            var list = new List<string>();
            var success = Friends?.Call("IsFriendOfS", playerId.ToString());
            if (success is string[])
            {
                return (success as string[]).ToList();
            }
            return list;
        }
        List<string> GetUniversalFriends(ulong playerId)
        {
            var success = Friends?.Call("GetFriendsReverse", playerId.ToString());
            if (success is string[])
            {
                return (success as string[]).ToList();
            }
            return new List<string>();
        }
        void OnFriendAdded(object playerId, object friendId)
        {
            AddFriend(friendId.ToString(), "FriendsAPI", playerId.ToString(), true);
        }
        void OnFriendRemoved(object playerId, object friendId)
        {
            RemoveFriend(friendId.ToString(), "FriendsAPI", playerId.ToString(), true);
        }
        #endregion

        #region Clans
        void GetClans()
        {
            if (Clans)
            {
                var allClans = Clans?.Call("GetAllClans");
                if (allClans != null && allClans is JArray)
                {
                    foreach(var clan in (JArray)allClans)
                    {
                        var name = clan.ToString();
                        List<string> members = GetClanMembers(name);
                        if (!clanData.ContainsKey(name))
                            clanData.Add(name, new List<string>());
                        clanData[name] = members;
                    }
                }
            }
        }
        string GetClan(ulong playerId)
        {
            string clanName = Clans?.Call<string>("GetClanOf", playerId);
            if (!string.IsNullOrEmpty(clanName))
            {
                if (!clanData.ContainsKey(clanName))
                    clanData.Add(clanName, GetClanMembers(clanName));
            }
            return clanName;
        }
        List<string> GetClanMembers(string clanTag)
        {
            var newList = new List<string>();
            var clan = instance.Clans?.Call("GetClan", clanTag);
            if (clan != null && clan is JObject)
            {                
                var members = (clan as JObject).GetValue("members");
                if (members != null && members is JArray)
                {
                    foreach (var member in (JArray)members)
                    {
                        newList.Add(member.ToString());
                    }
                }
            }
            return newList;
        }
        void OnClanCreate(string tag)
        {
            if (!clanData.ContainsKey(tag))
                clanData.Add(tag, GetClanMembers(tag));
        }
        void OnClanUpdate(string tag)
        {
            var members = GetClanMembers(tag);
            if (!clanData.ContainsKey(tag))                            
                clanData.Add(tag, members);            
            else
            {
                foreach (var member in clanData[tag])                                    
                    RemoveFriendList(member, "Clans", true); 
                foreach(var member in members)
                    AddFriendList(member, "Clans", members, true);
                clanData[tag] = members;
            }            
        }
        void OnClanDestroy(string tag)
        {            
            if (clanData.ContainsKey(tag))
            {
                foreach(var member in clanData[tag])                                    
                    RemoveFriendList(member, "Clans", true);                
                clanData.Remove(tag);
            }
        }
        #endregion
        #endregion

        #region Config        
        private ConfigData configData;        
        class ConfigData
        {
            [JsonProperty(PropertyName = "Friend Options")]
            public FriendOptions Friends { get; set; }
            [JsonProperty(PropertyName = "Marker Options")]
            public MapMarkers Markers { get; set; }
            [JsonProperty(PropertyName = "Map - Main Options")]
            public MapOptions Map { get; set; }
            [JsonProperty(PropertyName = "Map - Mini Options")]
            public Minimap MiniMap { get; set; }
            [JsonProperty(PropertyName = "Map - Complex Options")]
            public ComplexMap ComplexOptions { get; set; }
            [JsonProperty(PropertyName = "Spam Options")]
            public SpamOptions Spam { get; set; }

            public class FriendOptions
            {
                [JsonProperty(PropertyName = "Allow custom friend lists from other plugins")]
                public bool AllowCustomLists { get; set; }
                [JsonProperty(PropertyName = "Enable clans support")]
                public bool UseClans { get; set; }
                [JsonProperty(PropertyName = "Enable friends support")]
                public bool UseFriends { get; set; }
            }
            public class MapMarkers
            {
                [JsonProperty(PropertyName = "Show all players")]
                public bool ShowAllPlayers { get; set; }
                [JsonProperty(PropertyName = "Show caves")]
                public bool ShowCaves { get; set; }
                [JsonProperty(PropertyName = "Show debris")]
                public bool ShowDebris { get; set; }
                [JsonProperty(PropertyName = "Show friends and clanmates")]
                public bool ShowFriends { get; set; }
                [JsonProperty(PropertyName = "Show helicopters")]
                public bool ShowHelicopters { get; set; }
                [JsonProperty(PropertyName = "Show marker names")]
                public bool ShowMarkerNames { get; set; }
                [JsonProperty(PropertyName = "Show monuments")]
                public bool ShowMonuments { get; set; }
                [JsonProperty(PropertyName = "Show planes")]
                public bool ShowPlanes { get; set; }
                [JsonProperty(PropertyName = "Show self")]
                public bool ShowPlayer { get; set; }
                [JsonProperty(PropertyName = "Show supply drops")]
                public bool ShowSupplyDrops { get; set; }
                [JsonProperty(PropertyName = "Show vending machines (public broadcast only)")]
                public bool ShowPublicVendingMachines { get; set; }
                [JsonProperty(PropertyName = "Show cars (un-occupied only)")]
                public bool ShowCars { get; set; }
                [JsonProperty(PropertyName = "Show tanks")]
                public bool ShowTanks { get; set; }
            }
            public class MapOptions
            {
                [JsonProperty(PropertyName = "Enable AFK tracking")]
                public bool EnableAFKTracking { get; set; }
                [JsonProperty(PropertyName = "Hide event players")]
                public bool HideEventPlayers { get; set; }
                [JsonProperty(PropertyName = "Open map on for player's when they connect")]
                public bool StartOpen { get; set; }
                [JsonProperty(PropertyName = "Show map compass")]
                public bool ShowCompass { get; set; }
                [JsonProperty(PropertyName = "Map image options")]
                public MapImages MapImage { get; set; }                
                [JsonProperty(PropertyName = "Map update time (seconds)")]
                public float UpdateSpeed { get; set; }

                public class MapImages
                {
                    [JsonProperty(PropertyName = "Beancan.io API key (if applicable)")]
                    public string APIKey { get; set; }
                    [JsonProperty(PropertyName = "Use custom map")]
                    public bool CustomMap_Use { get; set; }
                    [JsonProperty(PropertyName = "Custom map filename")]
                    public string CustomMap_Filename { get; set; }
                }                
            }
            public class Minimap
            {
                [JsonProperty(PropertyName = "Enable the minimap")]
                public bool UseMinimap { get; set; }
                [JsonProperty(PropertyName = "Minimap horizontal scale")]
                public float HorizontalScale { get; set; }
                [JsonProperty(PropertyName = "Minimap vertical scale")]
                public float VerticalScale { get; set; }
                [JsonProperty(PropertyName = "Minimap docked on the left side of the screen")]
                public bool OnLeftSide { get; set; }
                [JsonProperty(PropertyName = "Minimap offset from side of the screen")]
                public float OffsetSide { get; set; }
                [JsonProperty(PropertyName = "Minimap offset from top of the screen")]
                public float OffsetTop { get; set; }                
            }
            public class ComplexMap
            {
                [JsonProperty(PropertyName = "Enable the complex map")]
                public bool UseComplexMap { get; set; }
                [JsonProperty(PropertyName = "Force complex zoom mode")]
                public bool ForceMapZoom { get; set; }
                [JsonProperty(PropertyName = "Forced zoom number (1, 2 or 3)")]
                public int ForcedZoomLevel { get; set; }
            }
            public class SpamOptions
            {
                [JsonProperty(PropertyName = "Allowed time between map changes")]
                public int TimeBetweenAttempts { get; set; }
                [JsonProperty(PropertyName = "Attempts before warning the user they are spamming")]
                public int WarningAttempts { get; set; }
                [JsonProperty(PropertyName = "Attempts before disabling the users map")]
                public int DisableAttempts { get; set; }
                [JsonProperty(PropertyName = "Amount of time a users map will be disabled")]
                public int DisableSeconds { get; set; }
                [JsonProperty(PropertyName = "Enable spam monitoring")]
                public bool Enabled { get; set; }
            }
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
                Friends = new ConfigData.FriendOptions
                {
                    AllowCustomLists = true,
                    UseClans = true,
                    UseFriends = true,
                },
                Markers = new ConfigData.MapMarkers
                {
                    ShowAllPlayers = false,
                    ShowCars = true,
                    ShowCaves = false,
                    ShowDebris = false,
                    ShowFriends = true,
                    ShowHelicopters = true,
                    ShowMarkerNames = true,
                    ShowMonuments = true,
                    ShowPlanes = true,
                    ShowPlayer = true,
                    ShowSupplyDrops = true,
                    ShowPublicVendingMachines = true,
                    ShowTanks = true
                },
                Map = new ConfigData.MapOptions
                {           
                    EnableAFKTracking = true,         
                    HideEventPlayers = true,
                    ShowCompass = true,
                    StartOpen = true,
                    MapImage = new ConfigData.MapOptions.MapImages
                    {
                        APIKey = "",
                        CustomMap_Filename = "",
                        CustomMap_Use = false
                    },                    
                    UpdateSpeed = 1f
                },
                MiniMap = new ConfigData.Minimap
                {                    
                    HorizontalScale = 1.0f,
                    VerticalScale = 1.0f,
                    OnLeftSide = true,
                    OffsetSide = 0,
                    OffsetTop = 0,
                    UseMinimap = true
                },
                ComplexOptions = new ConfigData.ComplexMap
                {
                    ForcedZoomLevel = 1,
                    ForceMapZoom = false,
                    UseComplexMap = true
                },
                Spam = new ConfigData.SpamOptions
                {
                    DisableAttempts = 10,
                    DisableSeconds = 120,
                    Enabled = true,
                    TimeBetweenAttempts = 3,
                    WarningAttempts = 5
                }
                             
            };
            SaveConfig(config);
        }
        private void LoadConfigVariables() => configData = Config.ReadObject<ConfigData>();
        void SaveConfig(ConfigData config) => Config.WriteObject(config, true);
        #endregion

        #region Data Management        
        void SaveMarkers()
        {
            markerData.WriteObject(storedMarkers);
        }
        void LoadData()
        {          
            try
            {
                storedMarkers = markerData.ReadObject<MarkerData>();
                customMarkers = storedMarkers.data;
            }
            catch
            {
                storedMarkers = new MarkerData();
            }
        }       
        class MarkerData
        {
            public Dictionary<string, MapMarker> data = new Dictionary<string, MapMarker>();
        }
        #endregion

        #region Image Storage
        private string GetImage(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            return ImageLibrary.GetImage(name, 0);            
        }        
        void ValidateImages()
        {
            Puts("[Warning] Validating imagery");
            if (isNewSave || !ImageLibrary.HasImage("mapimage", 0))
            {
                LoadImages();
                LoadMapImage();
            }
            else GenerateMaps(true, MapSettings.minimap, MapSettings.complexmap);
        }                
        
        private void LoadImages()
        {
            Puts("[Warning] Icon images have not been found. Uploading images to file storage");
                    
            string[] files = new string[] { "self", "friend", "other", "heli", "plane", "car", "tank" };
            string path = $"{dataDirectory}icons{Path.DirectorySeparatorChar}";

            Dictionary<string, string> newLoadOrder = new Dictionary<string, string>();
            foreach (string file in files)
            {                
                for (int i = 0; i <= 360; i = i + 10)
                    newLoadOrder.Add($"{file}{i}", $"{path}{file}{i}.png");                
            }

            newLoadOrder.Add("lighthouse", $"{path}lighthouse.png");
            newLoadOrder.Add("radtown", $"{path}radtown.png");
            newLoadOrder.Add("cave", $"{path}cave.png");
            newLoadOrder.Add("warehouse", $"{path}warehouse.png");
            newLoadOrder.Add("dish", $"{path}dish.png");
            newLoadOrder.Add("rocketfactory", $"{path}rocket.png");
            newLoadOrder.Add("spheretank", $"{path}spheretank.png");
            newLoadOrder.Add("harbor", $"{path}harbor.png");
            newLoadOrder.Add("special", $"{path}special.png");
            newLoadOrder.Add("supply", $"{path}supply.png");
            newLoadOrder.Add("debris", $"{path}debris.png");
            newLoadOrder.Add("vending", $"{path}vending.png");
            newLoadOrder.Add("gasstation", $"{path}gas.png");
            newLoadOrder.Add("supermarket", $"{path}market.png");
            newLoadOrder.Add("quarryhqm", $"{path}quarryhqm.png");
            newLoadOrder.Add("quarrystone", $"{path}quarrystone.png");
            newLoadOrder.Add("quarrysulfur", $"{path}quarrysulfur.png");
            newLoadOrder.Add("junkyard", $"{path}junkyard.png");

            foreach (var image in customMarkers)
            {
                string icon = image.Value.icon;
                if (icon != "special" && !newLoadOrder.ContainsKey(icon))
                {
                    if (!icon.StartsWith("http") && !icon.StartsWith("www") && !icon.StartsWith("file://"))
                        icon = $"{dataDirectory}custom{Path.DirectorySeparatorChar}{icon}.png";
                    newLoadOrder.Add(image.Value.icon, icon);
                }
            }
            ImageLibrary.ImportImageList(Title, newLoadOrder, 0, true);                
        } 
        private void LoadMapImage()
        {
            if (configData.Map.MapImage.CustomMap_Use)
            {
                Puts("[Warning] Downloading map image to file storage. Please wait!"); 
                ImageLibrary.AddImage(dataDirectory + configData.Map.MapImage.CustomMap_Filename, "mapimage_high", 0);
                ScaleMapImage();
                if (MapSettings.complexmap)
                {
                    Puts("[Warning] Attempting to split and store the complex mini-map. This may take a few moments! Failure to wait for this process to finish WILL result in error!");
                    AttemptSplit();
                }
                else GenerateMaps(true, MapSettings.minimap, false);
            }
            else DownloadMapImage();
        }       
        #endregion

        #region Map Generation - Credits to Calytic, Nogrod, kraz and beancan.io for the awesome looking map images and API to make this possible!
        void DownloadMapImage()
        {
            if (string.IsNullOrEmpty(configData.Map.MapImage.APIKey))
            {
                Puts("[Error] You must supply a valid API key to utilize the auto-download feature!\nVisit 'beancan.io' and register your server to retrieve your API key!");
                activated = false;
                return;
            }
            Puts("[Warning] Attempting to contact beancan.io to download your map image!");
            GetQueueID();            
        }
        void GetQueueID()
        {
            var url = $"http://beancan.io/map-queue-generate?level={level}&seed={mapSeed}&size={mapSize}&key={configData.Map.MapImage.APIKey}";
            webrequest.Enqueue(url, "", (code, response) =>
            {
                if (code != 200 || string.IsNullOrEmpty(response))
                {
                    if (code == 403)
                        PrintError($"Error: {code} - Invalid API key. Unable to download map image");
                    else Puts($"[Warning] Error: {code} - Couldn't get an answer from beancan.io. Unable to download map image. Please try again in a few minutes");                    
                }
                else CheckAvailability(response);
            }, this);
        }
        void CheckAvailability(string queueId)
        {
            webrequest.Enqueue($"http://beancan.io/map-queue/{queueId}", "", (code, response) =>
            {
                if (string.IsNullOrEmpty(response))
                {
                    Puts($"[Warning] Error: {code} - Couldn't get an answer from beancan.io");
                }
                else ProcessResponse(queueId, response);
            }, this);
        }
        void ProcessResponse(string queueId, string response)
        {
            switch (response)
            {
                case "-1":
                    Puts("[Warning] Your map is still in the queue to be generated. Checking again in 10 seconds");
                    break;
                case "0":
                    Puts("[Warning] Your map is still being generated. Checking again in 10 seconds");
                    break;
                case "1":
                    GetMapURL(queueId);
                    return;
                default:
                    Puts($"[Warning] Error retrieving map: Invalid response from beancan.io: Response code {response}");
                    return;
            }
            timer.Once(10, () => CheckAvailability(queueId));
        }
        void GetMapURL(string queueId)
        {
            var url = $"http://beancan.io/map-queue-image/{queueId}";
            webrequest.Enqueue(url, "", (code, response) =>
            {
                if (string.IsNullOrEmpty(response))
                {
                    Puts($"[Warning] Error: {code} - Couldn't get an answer from beancan.io");
                }
                else DownloadMap(response);
            }, this);
        }
        void DownloadMap(string url)
        {
            Puts("[Warning] Map generation successful! Downloading map image to file storage. Please wait!");
            ImageLibrary.AddImage(url, "mapimage_high", 0);
            ScaleMapImage();

            if (MapSettings.complexmap)
            {
                Puts("[Warning] Attempting to split and store the complex mini-map. This may take a while, please wait!");
                AttemptSplit();
            }
            else GenerateMaps(true, MapSettings.minimap, false);            
        }     
        void ScaleMapImage()
        {
            if (ImageLibrary.HasImage("mapimage_high", 0))
            {
                System.Drawing.Image image = mapSplitter.ImageFromStorage(uint.Parse(GetImage("mapimage_high")));
                var bytes = mapSplitter.ResizeImage(image, 1024);
                ImageLibrary.ImportImageData($"{instance.Title} - Map Image", new Dictionary<string, byte[]> { { "mapimage", bytes } }, 0, true);
            }
            else timer.In(1, ScaleMapImage);
        }   
        #endregion

        #region Map Splitter
        void AttemptSplit(int attempts = 0)
        {
            if (attempts == 5)
            {
                Puts("[Error] The plugin has timed out trying to find the map image to split! Complex map has been disabled");
                MapSettings.complexmap = false;                
                return;
            }
            if (ImageLibrary.HasImage("mapimage_high", 0))
            {
                var imageId = GetImage("mapimage_high");
                bool hasSplit = true;
                foreach (var amount in new int[] { 6, 12, 26 })
                {
                    if (!mapSplitter.SplitMap(imageId, amount))
                        hasSplit = false;
                }
                if (hasSplit)
                {
                    Puts("[Warning] Map split was successful!");
                    GenerateMaps(true, MapSettings.minimap, true);
                }
                else
                {
                    MapSettings.complexmap = false;
                    GenerateMaps(true, MapSettings.minimap, MapSettings.complexmap);
                }
            }
            else
            {
                Puts($"[Warning] Map image not found in file store. Waiting for 10 seconds and trying again (Attempt: {attempts + 1} / 5)");
                timer.Once(10, () => AttemptSplit(attempts + 1));
            }
        }        
        class MapSplitter
        {
            public bool SplitMap(string imageId, int amount)
            {
                System.Drawing.Image img = ImageFromStorage(uint.Parse(imageId));
                if (img == null)
                {
                    instance.Puts("[Error] Unable to load the map image from file storage. This may be caused by slow processing of the images being uploaded to your server. Wait for 5 minutes and reload the plugin. \nIf this problem persists after multiple attempts then unload the plugin and delete your ImageData.json data file or run the 'resetmap' command");
                    return false;
                }
                instance.Puts($"[Warning] Starting complex map split ({amount}x). Please wait!");
                Dictionary<string, byte[]> newLoadOrder = new Dictionary<string, byte[]>();

                int width = (int)(img.Width / (double)amount);
                int height = (int)(img.Height / (double)amount);

                int rowCount = 0;
                int colCount = 0;
                for (int r = 0; r < amount; r++)
                {
                    colCount = 0;
                    for (int c = 0; c < amount; c++)
                    {
                        var column = colCount;
                        var row = rowCount;
                        if (column < 1) column = 1;
                        if (column > amount - 2) column = amount - 2;
                        if (row < 1) row = 1;
                        if (row > amount - 2) row = amount - 2;

                        Bitmap cutPiece = new Bitmap(width * 3, height * 3);
                        System.Drawing.Graphics graphic = System.Drawing.Graphics.FromImage(cutPiece);
                        graphic.DrawImage(img, new Rectangle(0, 0, width * 3, height * 3), new Rectangle((width * column) - width, (height * row) - height, width * 3, height * 3), GraphicsUnit.Pixel);
                        graphic.Dispose();
                        colCount++;

                        byte[] array = ResizeImage(cutPiece, 256);
                        newLoadOrder.Add($"map-{amount}-{r}-{c}", array);                        
                    }
                    rowCount++;
                }
                instance.ImageLibrary.ImportImageData($"{instance.Title} - Complex ({amount})", newLoadOrder, 0, true);
                return true;
            }           
            public System.Drawing.Image ImageFromStorage(uint imageId)
            {
                byte[] imageData = FileStorage.server.Get(imageId, FileStorage.Type.png, CommunityEntity.ServerInstance.net.ID);
                System.Drawing.Image img = null;
                try
                {
                    img = (System.Drawing.Bitmap)((new System.Drawing.ImageConverter()).ConvertFrom(imageData));
                }
                catch (Exception ex)
                {
                    instance.PrintError($"Error whilst retrieving the map image from file storage: {ex.Message}\nIf you are running linux you must install LibGDIPlus using the following line: \"sudo apt install libgdiplus\", then restart your system for the changes to take affect");
                }
                return img;
            }
            public byte[] ResizeImage(Image image, int pixels)
            {
                var destRect = new Rectangle(0, 0, pixels, pixels);
                var destImage = new Bitmap(pixels, pixels);
                
                destImage.SetResolution(image?.HorizontalResolution ?? pixels, image?.VerticalResolution ?? pixels);

                using (var graphics = System.Drawing.Graphics.FromImage(destImage))
                {
                    graphics.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
                    graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                    graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                    graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;

                    using (var wrapMode = new System.Drawing.Imaging.ImageAttributes())
                    {
                        wrapMode.SetWrapMode(System.Drawing.Drawing2D.WrapMode.TileFlipXY);
                        graphics.DrawImage(image, destRect, 0, 0, image?.Width ?? pixels, image?.Height ?? pixels, GraphicsUnit.Pixel, wrapMode);
                    }
                }
                System.Drawing.ImageConverter converter = new System.Drawing.ImageConverter();
                byte[] array = (byte[])converter.ConvertTo(destImage, typeof(byte[]));
                return array;               
            }
        }
        #endregion

        #region Localization
        string msg(string key, string playerid = null) => lang.GetMessage(key, this, playerid);

        Dictionary<string, string> Messages = new Dictionary<string, string>
        {
            {"cpsN", "North" },
            {"cpsNE", "North-East" },
            {"cpsE", "East" },
            {"cpsSE", "South-East" },
            {"cpsS", "South" },
            {"cpsSW", "South-West" },
            {"cpsW", "West" },
            {"cpsNW", "North-West" },
            {"Plane", "Plane" },
            {"Supply Drop", "Supply Drop" },
            {"Helicopter", "Helicopter" },
            {"Car", "Car" },
            {"Tank", "Tank" },
            {"Debris", "Debris" },
            {"Vending", "Vending Machine" },
            {"lighthouse", "lighthouse" },
            {"radtown", "radtown" },
            {"spheretank", "spheretank" },
            {"big harbor", "big harbor" },
            {"small harbor", "small harbor" },
            {"gasstation", "gas station" },
            {"supermarket", "super market" },
            {"dish","dish" },
            {"warehouse","warehouse" },
            {"waterplant", "waterplant" },
            {"trainyard", "trainyard" },
            {"airfield", "airfield" },
            {"militarytunnel", "militarytunnel" },
            {"powerplant", "powerplant" },
            {"rocketfactory", "rocketfactory" },
            {"cave", "cave" },
            {"spamWarning", "Please do not spam the map. If you continue to do so your map will be temporarily disabled" },
            {"spamDisable", "Your map has been disabled for {0} seconds" },
            {"spamEnable", "Your map has been enabled" }
        };
        #endregion
    }
}