using System.Collections.Generic;
using System;
using System.Linq;
using UnityEngine;
using Oxide.Core.Plugins;
using Oxide.Core.Configuration;
using Oxide.Game.Rust.Cui;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Reflection;
using System.Globalization;

namespace Oxide.Plugins
{
    [Info("FactionsCore", "Absolut", "1.0.4", ResourceId = 2399)]

    class FactionsCore : RustPlugin
    {
        #region Fields

        [PluginReference]
        Plugin EventManager, ZoneManager, LustyMap, ZoneDomes, Kits, ImageLibrary, Professions, CustomSets, LastFactionStanding, FactionsTax, Conquest;
        static FieldInfo buildingPrivlidges = typeof(BasePlayer).GetField("buildingPrivilege", BindingFlags.Instance | BindingFlags.NonPublic);
        FactionsData fdata;
        private DynamicConfigFile FDATA;

        string TitleColor = "<color=orange>";
        string MsgColor = "<color=#A9A9A9>";

        bool initialized;
        public HashSet<FactionPlayer> FactionPlayers = new HashSet<FactionPlayer>();
        private List<ulong> InFactionChat = new List<ulong>();
        private List<ulong> MakingFactionAnnouncement = new List<ulong>();
        private List<ulong> AdminView = new List<ulong>();
        private List<ulong> JoinCooldown = new List<ulong>();
        private List<ulong> Passthrough = new List<ulong>();
        private Dictionary<string, List<ulong>> ActiveUniforms = new Dictionary<string, List<ulong>>();
        private Dictionary<ulong, ulong> AssignFaction = new Dictionary<ulong, ulong>();
        private Dictionary<ulong, Dictionary<Item, Slot>> LockedUniform = new Dictionary<ulong, Dictionary<Item, Slot>>();
        private List<Monuments> MonumentLocations = new List<Monuments>();
        class Monuments
        {
            public Vector3 position;
            public float radius;
        }
        private Dictionary<string, Timer> timers = new Dictionary<string, Timer>();
        private Dictionary<ulong, FactionDesigner> FactionDetails = new Dictionary<ulong, FactionDesigner>();
        private Dictionary<ulong, FactionDesigner> FactionEditor = new Dictionary<ulong, FactionDesigner>();
        #endregion

        #region Hooks   

        void Loaded()
        {
            FDATA = Interface.Oxide.DataFileSystem.GetFile("FactionsCore_Data");
            lang.RegisterMessages(messages, this);
        }

        void Unload()
        {
            foreach (var p in FactionPlayers)
                DestroyFactionPlayer(p, true);
            foreach (var entry in timers)
                entry.Value.Destroy();
            timers.Clear();
            DestroyAll<FactionPlayer>();
            FactionPlayers.Clear();
            SaveData();
        }

        private void DestroyAll<T>()
        {
            var objects = UnityEngine.Object.FindObjectsOfType(typeof(T));
            if (objects == null) return;
            foreach (var gameObj in objects)
                UnityEngine.Object.Destroy(gameObj);
        }

        void OnServerInitialized()
        {
            initialized = false;
            try
            {
                ImageLibrary.Call("isLoaded", null);
            }
            catch (Exception)
            {
                PrintWarning("No Image Library.. load ImageLibrary to use this Plugin", Name);
                Interface.Oxide.UnloadPlugin(Name);
                return;
            }
            LoadVariables();
            LoadData();
            permission.RegisterPermission(this.Title + ".admin", this);
            permission.RegisterPermission(this.Title + ".allow", this);
            foreach (BasePlayer p in BasePlayer.activePlayerList)
                OnPlayerInit(p);
            InitializeStuff();
            CheckFactionStale();
        }

        [ChatCommand("testplayers")]
        void chatTP(BasePlayer player)
        {
            var i = 0;
            while (i < 100)
            {
                ulong FakeID = (ulong)GetRandomNumber();
                if (fdata.Players.ContainsKey(FakeID)) continue;
                fdata.Players.Add(FakeID, new PlayerData { });
                i++;
            }
            SendReply(player, "Fake Players Created!");
            SaveData();
        }


        private void InitializeStuff()
        {
            if (timers.ContainsKey("imageloading"))
            {
                timers["imageloading"].Destroy();
                timers.Remove("imageloading");
            }
            if (!isReady())
            { Puts(GetMSG("WaitingImageLibrary")); timers.Add("imageloading", timer.Once(60, () => InitializeStuff())); return; };
            CreateLoadOrder();
            foreach (var entry in configData.FactionEmblems_URLS)
                AddImage(entry.Value, "Embleem" + entry.Key, (ulong)ResourceId);
            if (CustomSets)
                foreach (var entry in fdata.Factions)
                    if (!string.IsNullOrEmpty(entry.Value.KitorSet))
                        SaveSetContents(entry.Value.KitorSet);
            FindMonuments();
            initialized = true;
            SaveData();
            timers.Add("info", timer.Once(900, () => InfoLoop()));
            timers.Add("save", timer.Once(600, () => SaveLoop()));
            timers.Add("ui", timer.Once(60, () => ReloadPlayerPanel()));
        }

        public void SaveSetContents(string set)
        {
            var contents = CustomSets?.Call("GetSetInfo", set);
            if (contents is bool || contents == null)
            {
                Puts($"Set: {set} is not valid!");
                return;
            }
            JObject kitContents = contents as JObject;
            JArray items = kitContents["belt"] as JArray;
            foreach (var itemEntry in items)
            {
                JObject item = itemEntry as JObject;
                if (!ActiveUniforms.ContainsKey((string)item["shortname"]))
                    ActiveUniforms.Add((string)item["shortname"], new List<ulong>());
                if (!ActiveUniforms[(string)item["shortname"]].Contains((ulong)item["skin"]))
                    ActiveUniforms[(string)item["shortname"]].Add((ulong)item["skin"]);

            }
            items = kitContents["wear"] as JArray;
            foreach (var itemEntry in items)
            {
                JObject item = itemEntry as JObject;
                if (!ActiveUniforms.ContainsKey((string)item["shortname"]))
                    ActiveUniforms.Add((string)item["shortname"], new List<ulong>());
                if (!ActiveUniforms[(string)item["shortname"]].Contains((ulong)item["skin"]))
                    ActiveUniforms[(string)item["shortname"]].Add((ulong)item["skin"]);
            }
            items = kitContents["main"] as JArray;
            foreach (var itemEntry in items)
            {
                JObject item = itemEntry as JObject;
                if (!ActiveUniforms.ContainsKey((string)item["shortname"]))
                    ActiveUniforms.Add((string)item["shortname"], new List<ulong>());
                if (!ActiveUniforms[(string)item["shortname"]].Contains((ulong)item["skin"]))
                    ActiveUniforms[(string)item["shortname"]].Add((ulong)item["skin"]);
            }
        }


        private void CreateLoadOrder()
        {
            Dictionary<string, string> newLoadOrder = new Dictionary<string, string>
            {
            {     "announcementoff", "http://i.imgur.com/JcyGEid.png" },
            {    "announcementon", "http://i.imgur.com/JuHJViL.png"  },
            {    "chatoff", "http://i.imgur.com/ZnXH99w.png"  },
            {    "chaton", "http://i.imgur.com/x80bOhZ.png"  },
            {    "mainmenu", "http://i.imgur.com/6lNt3SU.png"  },
            {    "lightbutton",  "http://i.imgur.com/rUhwglx.jpg" },
            {     "darkbutton", "http://i.imgur.com/91KexGA.jpg" },
            {     "hourglass", "http://i.imgur.com/4ItihIf.png" },
            {     "self", "http://i.imgur.com/w3iZpsl.png" },
            {     "box", "http://i.imgur.com/KWN4edk.png" },
            {     "battle", "http://i.imgur.com/EdiuVTZ.png" },
            {     "safezone", "http://i.imgur.com/v5TPxyv.png" },
            {     "friend", "http://i.imgur.com/FE4fV9O.png" },
            {     "greylongbutton", "http://www.pd4pic.com/images/glass-glossy-gui-shape-element-rectangle-shapes.png" },
            {     "first", "http://cdn.mysitemyway.com/etc-mysitemyway/icons/legacy-previews/icons/simple-black-square-icons-arrows/126517-simple-black-square-icon-arrows-double-arrowhead-left.png" },
            {     "back", "http://i.imgur.com/3iCM9zg.png" },
            {     "next",  "http://i.imgur.com/Dt0XmUP.png"},
            {    "last","http://cdn.mysitemyway.com/etc-mysitemyway/icons/legacy-previews/icons/matte-white-square-icons-arrows/124577-matte-white-square-icon-arrows-double-arrowhead-right.png"   },
            {     "neverdelete", "http://www.intrawallpaper.com/static/images/r4RtXBr.png" },
            { "bluelongbutton",  "https://pixabay.com/static/uploads/photo/2016/01/23/11/41/button-1157299_960_720.png"    },
            {    "redlongbutton", "https://pixabay.com/static/uploads/photo/2016/01/23/11/42/button-1157301_960_720.png"  },
            { "blacklongbutton","https://pixabay.com/static/uploads/photo/2016/01/23/11/26/button-1157269_960_720.png"      },
            {    "greenlongbutton","https://pixabay.com/static/uploads/photo/2015/07/25/08/03/the-button-859349_960_720.png"   },
            {    "purplelongbutton",  "https://pixabay.com/static/uploads/photo/2015/07/25/07/55/the-button-859343_960_720.png" },
            {     "greensquarebutton", "http://www.pd4pic.com/images/libya-flag-country-nationality-square-button.png" },
            {     "redsquarebutton", "https://openclipart.org/image/2400px/svg_to_png/78601/Red-button.png" },
            {    "circleselection", "http://i.imgur.com/mkjxZiu.png" }
            };
            ImageLibrary.Call("ImportImageList", Title, newLoadOrder, (ulong)ResourceId, true);
        }


        void CheckFactionStale()
        {
            if (configData.FactionStaleTime == 0) return;
            List<ushort> StaleFactions = new List<ushort>();
            foreach (var entry in fdata.Factions)
                if (entry.Value.LastMemberLoggedIn != 0)
                    if ((entry.Value.LastMemberLoggedIn + (configData.FactionStaleTime * 86400)) < GrabCurrentTime())
                        StaleFactions.Add(entry.Key);
            foreach (var entry in StaleFactions)
                fdata.Factions.Remove(entry);
        }

        private void OnPlayerInit(BasePlayer player)
        {
            if (player != null)
            {
                if (player.HasPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot) || !initialized)
                {
                    timer.Once(5, () => OnPlayerInit(player));
                    return;

                }
                if (player.IsSleeping())
                    player.EndSleeping();
                InitializeFactionPlayer(player);
            }
        }

        private void OnPlayerRespawned(BasePlayer player)
        {
            DestroyUI(player);
                GiveFactionGear(player);
                PlayerPanel(player);
        }

        private void OnPlayerDisconnected(BasePlayer player)
        {
            if (GetFactionPlayer(player) != null)
            {
                if (timers.ContainsKey(player.userID.ToString()))
                    timers.Remove(player.userID.ToString());
                DestroyFactionPlayer(GetFactionPlayer(player));
            }
        }
        private void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo hitInfo)
        {
            if (hitInfo == null || entity == null) return;
            try
            {
                if (configData.Allow_FriendlyFire) return;
                if (entity is BasePlayer && hitInfo.Initiator is BaseTrap)
                    if (fdata.Players[entity.ToPlayer().userID].faction == fdata.Players[hitInfo.Initiator.OwnerID].faction)
                        hitInfo.damageTypes.ScaleAll(configData.FriendlyFire_DamageScale);
                var attacker = hitInfo.Initiator.ToPlayer();
                if (attacker == null || GetFactionPlayer(attacker) == null) return;
                if (entity is BasePlayer)
                {
                    var victim = entity.ToPlayer();
                    if (victim == null) return;
                    if (fdata.Players.ContainsKey(victim.userID))
                        if (fdata.Players[victim.userID].faction == 0)
                            return;
                    if (EventManager)
                    {
                        object isPlaying = EventManager?.Call("isPlaying", new object[] { attacker });
                        if (isPlaying is bool)
                            if ((bool)isPlaying)
                                return;
                    }
                    if (victim != attacker)
                    {
                        if (GetFactionPlayer(attacker).Faction != fdata.Players[victim.userID].faction) return;
                        {
                            hitInfo.damageTypes.ScaleAll(configData.FriendlyFire_DamageScale);
                            if (timers.ContainsKey(attacker.UserIDString + "FF")) return;
                            GetSendMSG(attacker, "FFs", victim.displayName);
                            timers.Add(attacker.UserIDString, timer.Once(60, () => timers.Remove(attacker.UserIDString + "FF")));
                        }
                    }
                }
                else if (entity is BaseEntity)
                {
                    var OwnerID = entity.OwnerID;
                    if (attacker.userID == OwnerID) return;
                    if (GetFactionPlayer(attacker).Faction != fdata.Players[OwnerID].faction) return;
                    if (OwnerID != 0)
                    {
                        if (EventManager)
                        {
                            object isPlaying = EventManager?.Call("isPlaying", new object[] { attacker });
                            if (isPlaying is bool)
                                if ((bool)isPlaying)
                                    return;
                        }
                        if (!AllowedToBuild(attacker))
                        {
                            hitInfo.damageTypes.ScaleAll(0);
                            if (timers.ContainsKey(attacker.UserIDString + "FFB")) return;
                            GetSendMSG(attacker, "FFBuildings", GetDisplayName(OwnerID));
                            timers.Add(attacker.UserIDString, timer.Once(60, () => timers.Remove(attacker.UserIDString + "FFB")));
                        }
                    }
                }
            }
            catch (Exception)
            {
            }
        }

        [ConsoleCommand("UI_DestroyUI")]
        private void cmdUI_DestroyUI(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            DestroyUI(player);
        }


        void DestroyFactionPlayer(FactionPlayer player, bool unloading = false)
        {
            if (player.player == null) return;
            if (!string.IsNullOrEmpty(configData.MenuKeyBinding))
                player.player.Command($"bind {configData.MenuKeyBinding} \"\"");
            DestroyUI(player.player, true);
            SaveFactionPlayer(player);
            if(IsFaction(player.Faction))
                fdata.Factions[player.Faction].LastMemberLoggedIn = GrabCurrentTime();
            if (!unloading)
            {
                if (FactionPlayers.Contains(player))
                    FactionPlayers.Remove(player);
                UnityEngine.Object.Destroy(player);
            }
        }

        static double GrabCurrentTime() => DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1, 0, 0, 0)).TotalSeconds;


        object OnServerCommand(ConsoleSystem.Arg arg)
        {
            if (!initialized) return null;
            var player = arg.Connection?.player as BasePlayer;
            if (player == null || arg.Args == null || arg.Args[0].StartsWith("/") || arg.cmd?.FullName != "chat.say") return null;
            if (FactionDetails.ContainsKey(player.userID))
            {
                FactionCreationChat(player, arg.Args);
                return false;
            }

            if (!configData.Use_FactionAnnouncements && !configData.Use_PrivateFactionChat && !configData.Use_FactionChatControl) return null;
            string message = string.Join(" ", arg.Args);
            if (message.Length == 0)
                return null;
            string color = "";
            if (GetFactionPlayer(player) != null && GetFactionPlayer(player).Faction != 0 && fdata.Factions.ContainsKey(GetFactionPlayer(player).Faction))
            {
                var faction = GetFactionPlayer(player).Faction;
                if (configData.Use_FactionAnnouncements && MakingFactionAnnouncement.Contains(player.userID))
                {
                    fdata.Factions[faction].FactionAnnouncements.Add(fdata.Factions[faction].FactionAnnouncements.Keys.Max() + 1, message);
                    foreach (var fp in FactionPlayers.Where(k => k.Faction == faction && k.open))
                        FactionAnnouncementPanel(fp.player);
                    return false;
                }
                color = "<color=" + fdata.Factions[faction].ChatColor + ">";
                if (configData.Use_FactionTags)
                {
                    color += "[" + fdata.Factions[faction].tag + "] ";
                }
                string formatMsg = "";
                if (configData.Use_PrivateFactionChat && InFactionChat.Contains(player.userID))
                {
                    formatMsg = color + player.displayName + "</color>: " + $"<color={configData.InFactionChat_ChatColor}>" + message + "</color>";
                    BroadcastFaction(player, formatMsg);
                }
                else if (configData.Use_FactionChatControl)
                {
                    formatMsg = color + player.displayName + "</color>: " + message;
                    Broadcast(formatMsg, player.userID.ToString());
                }
                return false;
            }
            return null;
        }


        private void FactionCreationChat(BasePlayer player, string[] Args)
        {
            FactionDesigner Creation = FactionDetails[player.userID];
            if (Args.Contains("quit"))
            {
                QuitFactionCreation(player);
                return;
            }
            if (Args.Contains("save"))
            {
                SaveFaction(player);
                return;
            }
            var args = string.Join(" ", Args);
            if (args.Length == 0) return;
            switch (Creation.stepNum)
            {
                case 1:
                    if (fdata.Factions.Count() > 0)
                    {
                        foreach (var faction in fdata.Factions)
                            if (faction.Value.Name == args)
                            {
                                GetSendMSG(player, "FactionNameExists", args);
                                break;
                            }
                            else
                            {
                                Creation.faction.Name = args;
                                Creation.stepNum = 2;
                                if (Creation.editing)
                                    FactionCreation(player, 20);
                                else FactionCreation(player, 2);
                            }
                    }
                    else
                    {
                        Creation.faction.Name = args;
                        Creation.stepNum = 2;
                        if (Creation.editing)
                            FactionCreation(player, 20);
                        else FactionCreation(player, 2);
                    }
                    return;
                case 2:
                    if (fdata.Factions.Count() > 0)
                    {
                        if (Args[0].Length > 5)
                        {
                            GetSendMSG(player, "FactionTagToLong", Args[0]);
                            return;
                        }
                        foreach (var faction in fdata.Factions)
                            if (faction.Value.tag == Args[0])
                            {
                                GetSendMSG(player, "FactionTagExists", Args[0]);
                            }
                            else
                            {
                                Creation.faction.tag = $"{Args[0]}";
                                Creation.stepNum = 3;
                                if (Creation.editing)
                                    FactionCreation(player, 20);
                                else FactionCreation(player, 3);
                            }
                    }
                    else
                    {
                        Creation.faction.tag = $"[{Args[0]}]";
                        Creation.stepNum = 3;
                        if (Creation.editing)
                            FactionCreation(player, 20);
                        else FactionCreation(player, 3);
                    }
                    return;
                case 3:
                    if (Creation.faction.description == "")
                        Creation.faction.description = args;
                    else Creation.faction.description = Creation.faction.description + " " + args;
                    FactionCreation(player, 6);
                    return;
            }
        }

        #endregion

        #region Functions
        bool isAuth(BasePlayer player)
        {
            if (player.net.connection != null)
                if (player.net.connection.authLevel < 2 && !permission.UserHasPermission(player.UserIDString, "FactionsCore.admin"))
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

        public object LFSLocation(ushort ID) => LastFactionStanding?.Call("LFSLocation", ID);
        public object TaxboxLocation(ushort ID) => FactionsTax?.Call("TaxboxLocation", ID);

        public List<string> GetSetContents(string set)
        {
            if (CustomSets?.Call("GetSetContents", set) is bool) return null;
            return (List<string>)CustomSets?.Call("GetSetContents", set);
        }

        private void InitializeFactionPlayer(BasePlayer player)
        {
            if (GetFactionPlayer(player) == null)
            FactionPlayers.Add(player.gameObject.AddComponent<FactionPlayer>());
            FactionPlayer fp = player.GetComponent<FactionPlayer>();
            if (!fdata.Players.ContainsKey(player.userID))
                fdata.Players.Add(player.userID, new PlayerData());
            var data = fdata.Players[player.userID];
            if (fdata.Factions.ContainsKey(data.faction))
            {
                if (fdata.Factions[data.faction].factionPlayers.Contains(player.userID))
                {
                    fp.Faction = data.faction;
                    if (fdata.Factions[data.faction].factionPlayers.Count == 1)
                        fdata.Factions[data.faction].Leader = player.userID;
                    ZoneManager?.Call("AddPlayerToZoneWhitelist", data.faction.ToString(), player);
                    AuthorizePlayerOnTurrets(player);
                }
                else
                {
                    ZoneManager?.Call("RemovePlayerFromZoneWhitelist", data.faction.ToString(), player);
                    UNAuthorizePlayerOnTurrets(player, data.faction);
                    fp.Faction = 0;
                }
            }
            else
                fp.Faction = 0;
            if (fp.Faction != 0)
                AddFactionListLM(player.userID, fdata.Factions[fp.Faction].Name, fdata.Factions[fp.Faction].factionPlayers);
            string key = String.Empty;
            if (!string.IsNullOrEmpty(configData.MenuKeyBinding))
            {
                player.Command($"bind {configData.MenuKeyBinding} \"UI_FC_ToggleMenu\"");
                key = GetMSG("FCAltInfo", player, configData.MenuKeyBinding.ToUpper());
            }
            if (configData.InfoInterval != 0)
                GetSendMSG(player, "FCInfo", key);
            GiveFactionGear(player);
            PlayerPanel(player);
            PrivateCheck(player.userID);
            SaveData();
        }

        private void GiveFactionGear(BasePlayer player)
        {
            if (LockedUniform.ContainsKey(player.userID))
            {
                foreach (var entry in LockedUniform[player.userID])
                {
                    if (!Passthrough.Contains(player.userID)) Passthrough.Add(player.userID);
                    entry.Key.RemoveFromContainer();
                    entry.Key.Remove(0f);
                }
                LockedUniform.Remove(player.userID);
            }
            if (GetFactionPlayer(player) == null || GetFactionPlayer(player).Faction == 0 || string.IsNullOrEmpty(fdata.Factions[GetFactionPlayer(player).Faction].KitorSet)) return;
            if (Kits)
            {
                object isKit = Kits?.Call("isKit", new object[] { fdata.Factions[GetFactionPlayer(player).Faction].KitorSet });
                if (isKit is bool)
                    if ((bool)isKit)
                    {
                        List<Item> current = new List<global::Item>();
                        current = player.inventory.containerWear.itemList.Where(k => k.IsLocked()).Select(k => k).ToList();
                        if (current != null)
                            foreach (var entry in current)
                            {
                                entry.RemoveFromContainer();
                            }
                        if (!Passthrough.Contains(player.userID)) Passthrough.Add(player.userID);
                        Kits?.Call("GiveKit", player, fdata.Factions[GetFactionPlayer(player).Faction].KitorSet);
                        if (Passthrough.Contains(player.userID)) Passthrough.Remove(player.userID);
                        LockedUniform.Add(player.userID, new Dictionary<global::Item, Slot>());
                        foreach (var entry in player.inventory.containerWear.itemList)
                        {
                            if (!ActiveUniforms.ContainsKey(entry.info.shortname))
                                ActiveUniforms.Add(entry.info.shortname, new List<ulong>());
                            if (!ActiveUniforms[entry.info.shortname].Contains(entry.skin))
                                ActiveUniforms[entry.info.shortname].Add(entry.skin);
                            Slot slot;
                            if (ItemSlots.TryGetValue(entry.info.shortname, out slot))
                                LockedUniform[player.userID].Add(entry, slot);
                        }
                        foreach (var entry in current) entry.MoveToContainer(player.inventory.containerWear);
                        return;

                    }
            }
            else if (CustomSets)
            {
                if ((bool)CustomSets?.Call("isSet", fdata.Factions[GetFactionPlayer(player).Faction].KitorSet))
                {
                    List<Item> current = new List<global::Item>();
                    current = player.inventory.containerWear.itemList.Select(k => k).ToList();
                    if (current != null)
                        foreach (var entry in current)
                        {
                            entry.RemoveFromContainer();
                        }
                    if (!Passthrough.Contains(player.userID)) Passthrough.Add(player.userID);
                    CustomSets?.Call("GiveSet", player, fdata.Factions[GetFactionPlayer(player).Faction].KitorSet);
                    if (Passthrough.Contains(player.userID)) Passthrough.Remove(player.userID);
                    LockedUniform.Add(player.userID, new Dictionary<global::Item, Slot>());
                    foreach (var entry in player.inventory.containerWear.itemList)
                    {
                        if (!ActiveUniforms.ContainsKey(entry.info.shortname))
                            ActiveUniforms.Add(entry.info.shortname, new List<ulong>());
                        if (!ActiveUniforms[entry.info.shortname].Contains(entry.skin))
                            ActiveUniforms[entry.info.shortname].Add(entry.skin);
                        Slot slot;
                        if (ItemSlots.TryGetValue(entry.info.shortname, out slot))
                            LockedUniform[player.userID].Add(entry, slot);
                    }
                    foreach (var entry in current)
                    {
                        if (LockedUniform[player.userID].Select(k => k.Key).Where(k => k.info.shortname == entry.info.shortname && k.skin == entry.skin) != null)
                        {
                            entry.Remove(0f);
                        }
                        else
                            entry.MoveToContainer(player.inventory.containerWear);
                    }
                    //player.inventory.containerWear.MarkDirty();
                    return;
                }
            }
        }

        object CanWearItem(PlayerInventory inventory, Item item)
        {
            if (!initialized) return null;
            //Puts($"ITEM: {item.info.displayName.translated}");
            if (!configData.LockFactionKits_and_CustomSets) return null;
            if (inventory.containerWear.playerOwner == null) return null;
            BasePlayer player = inventory.containerBelt.playerOwner;
            if (player == null) return null;
            if (Passthrough.Contains(player.userID))
                return null;
            var fp = GetFactionPlayer(player);
            if (!LockedUniform.ContainsKey(player.userID))
                if (!ActiveUniforms.ContainsKey(item.info.shortname) || !ActiveUniforms[item.info.shortname].Contains(item.skin))
                {
                    //Puts("Not restricted item and player doesnt have locked item");
                    return null;
                }
            if (fp == null || fp.Faction == 0 || !LockedUniform.ContainsKey(player.userID))
            {
                //Puts("Restricted item and player is not in faction or have locked items");
                return false;
            }
            //Puts("Locked Uniform Contains Player - Not passthrough");
            if (LockedUniform[player.userID].ContainsKey(item))
            {
                //Puts("Item is approved for Wear List... allowed through");
                return null;
            }
            //Puts("Locked doesnt contain the item...");
            Slot slot;
            if (ItemSlots.TryGetValue(item.info.shortname, out slot))
            {
                //Puts("ItemSlots has item...");
                if (LockedUniform[player.userID].ContainsValue(slot) || slot == Slot.any)
                {
                    //Puts("Slot is any or the same as a locked item...");
                    return false;
                }
            }
            //Puts("ALLOWED");
            return null;
        }

        object OnItemAction(Item item, string cmd)
        {
            if (!initialized) return null;
            if (item == null || item.parent == null || item.parent.playerOwner == null) return null;
            BasePlayer player = item.parent.playerOwner.GetComponent<PlayerInventory>().containerWear.GetOwnerPlayer();
            if (player == null) return null;
            if (item.parent.playerOwner.inventory.containerWear.itemList.Contains(item) && LockedUniform[player.userID].ContainsKey(item) && cmd == "drop")
                return false;
            return null;
        }

        object CanAcceptItem(ItemContainer container, Item item)
        {
            if (!initialized) return null;
            if (!configData.LockFactionKits_and_CustomSets) return null;
            if (container == null || item == null || item.parent == null || item.parent.playerOwner == null) return null;
            //container.playerOwner == null || container.playerOwner.GetComponent<PlayerInventory>() == null ||
            BasePlayer player = item.parent.playerOwner.GetComponent<PlayerInventory>().containerBelt.GetOwnerPlayer();
            if (Passthrough.Contains(player.userID) || !LockedUniform.ContainsKey(player.userID)) return null;
            //Puts("Locked Uniform Contains Player");
            //if (LockedUniform[player.userID].ContainsKey(item) && container == player.inventory.containerWear)
            //{
            //    Puts("Item is approved for Wear List... allowed through");
            //    return null;
            //}

            if (item.parent.playerOwner.inventory.containerWear.itemList.Contains(item) && LockedUniform[player.userID].ContainsKey(item))
            {
                //Puts("Item is in Wear List and Locked contains the item...");
                return ItemContainer.CanAcceptResult.CannotAccept;
            }
            //Puts("Item is not in Wear List or Locked doesnt contain the item...");
            //Slot slot;
            //if (ItemSlots.TryGetValue(item.info.shortname, out slot))
            //{
            //    Puts("ItemSlots has item...");
            //    if (LockedUniform[player.userID].ContainsValue(slot) || slot == Slot.any)
            //    {
            //        Puts("Slot is any or the same or locked uniforms contains it...");
            //        return ItemContainer.CanAcceptResult.CannotAccept;
            //    }
            //}
            return null;
        }

        private void FindMonuments()
        {
            var allobjects = UnityEngine.Object.FindObjectsOfType<GameObject>();
            foreach (var gobject in allobjects)
                if (gobject.name.Contains("autospawn/monument"))
                    MonumentLocations.Add(new Monuments { position = gobject.transform.position, radius = gobject.transform.GetBounds().max.z });
        }

        //AutoTurrets
        void OnEntitySpawned(BaseNetworkable entity)
        {
            if (!initialized) return;
            if (entity == null) return;
            if (entity as AutoTurret != null && configData.AutoAuthorization)
            {
                var userid = entity.GetComponent<AutoTurret>().OwnerID;
                BasePlayer owner = BasePlayer.FindByID(userid);
                if (owner == null || GetFactionPlayer(owner) == null || GetFactionPlayer(owner).Faction == 0) return;
                AssignTurretAuth(fdata.Factions[GetFactionPlayer(owner).Faction], entity.GetComponent<AutoTurret>());
            }
        }

        object CanUseLockedEntity(BasePlayer player, BaseLock door)
        {
            if (!initialized) return null;
            if (GetFactionPlayer(player) == null || GetFactionPlayer(player).Faction == 0) return null;
            var parent = door.parentEntity.Get(true);
            var prefab = parent.LookupPrefab();
            if (parent.IsOpen()) return true;
            if (configData.AutoAuthorization)
            {
                if (configData.AuthorizeLeadersOnly && fdata.Factions[GetFactionPlayer(player).Faction].Leader != player.userID) return null;
                if (fdata.Factions[GetFactionPlayer(player).Faction].factionPlayers.Contains(parent.OwnerID) && AllowedToBuild(player)) return true;
            }
            return null;
        }

        void AssignTurretAuth(Faction faction, AutoTurret turret)
        {
            if (!configData.AutoAuthorization) return;
            if (configData.AuthorizeLeadersOnly)
            {
                var owner = faction.Leader;
                if (!turret.authorizedPlayers.Contains(new ProtoBuf.PlayerNameID() { userid = owner, username = GetDisplayName(owner) }))
                    turret.authorizedPlayers.Add(new ProtoBuf.PlayerNameID() { userid = owner, username = GetDisplayName(owner) });
                return;
            }
            foreach (var p in faction.factionPlayers)
            {
                if (!fdata.Players.ContainsKey(p)) continue;
                if (!turret.authorizedPlayers.Contains(new ProtoBuf.PlayerNameID() { userid = p, username = GetDisplayName(p) }))
                    turret.authorizedPlayers.Add(new ProtoBuf.PlayerNameID() { userid = p, username = GetDisplayName(p) });
            }
        }

        void AuthorizePlayerOnTurrets(BasePlayer player)
        {
            var faction = fdata.Factions[GetFactionPlayer(player).Faction];
            if (configData.AuthorizeLeadersOnly && faction.Leader != player.userID) return;
            if (configData.AutoAuthorization)
            {
                List<AutoTurret> turrets = new List<AutoTurret>();
                turrets = BaseNetworkable.serverEntities.Where(k => (k as AutoTurret) != null && faction.factionPlayers.Contains((k as AutoTurret).OwnerID)).Select(k => k as AutoTurret).ToList();
                foreach (var entry in turrets)
                {
                    var turret = entry as AutoTurret;
                    if (!turret.authorizedPlayers.Contains(new ProtoBuf.PlayerNameID() { userid = player.userID, username = player.displayName }))
                        turret.authorizedPlayers.Add(new ProtoBuf.PlayerNameID() { userid = player.userID, username = player.displayName });
                }
            }
        }

        void UNAuthorizePlayerOnTurrets(BasePlayer player, ushort oldFaction)
        {
            var faction = fdata.Factions[oldFaction];
            if (configData.AutoAuthorization)
            {
                List<BaseNetworkable> turrets = BaseNetworkable.serverEntities.Where(k => (k as AutoTurret) != null && faction.factionPlayers.Contains((k as AutoTurret).OwnerID)).Select(k => k).ToList();
                {
                    if (turrets != null)
                        foreach (var entry in turrets)
                        {
                            var turret = entry as AutoTurret;
                            if (turret.authorizedPlayers.Contains(new ProtoBuf.PlayerNameID() { userid = player.userID, username = player.displayName }))
                                turret.authorizedPlayers.Remove(new ProtoBuf.PlayerNameID() { userid = player.userID, username = player.displayName });
                        }
                }
            }
        }


        void SaveFactionPlayer(FactionPlayer player)
        {
            if (fdata.Factions.ContainsKey(player.Faction))
                if (!fdata.Factions[player.Faction].factionPlayers.Contains(player.player.userID))
                    fdata.Factions[player.Faction].factionPlayers.Add(player.player.userID);
            if (!fdata.Players.ContainsKey(player.player.userID))
                fdata.Players.Add(player.player.userID, new PlayerData());
            var fp = fdata.Players[player.player.userID];
            fp.faction = player.Faction;
        }

        public FactionPlayer GetFactionPlayer(BasePlayer player)
        {
            if (!player.GetComponent<FactionPlayer>()) return null;
            else return player.GetComponent<FactionPlayer>();
        }

        bool isOwner(BasePlayer player, ushort faction)
        {
            if (fdata.Factions[faction].Leader == player.userID)
                return true;
            return false;
        }

        bool isModerator(BasePlayer player, ushort faction)
        {
            if (fdata.Factions[faction].Moderators.Contains(player.userID))
                return true;
            return false;
        }

        public void Broadcast(string message, string userid = "0") => PrintToChat(message);

        public void BroadcastFaction(BasePlayer source, string message, ushort faction = 0)
        {
            if (faction == 0)
                faction = GetFactionPlayer(source).Faction;
            if (!fdata.Factions.ContainsKey(faction)) return;
            string color = fdata.Factions[faction].ChatColor;
            foreach (var entry in fdata.Factions[faction].factionPlayers)
                try { BasePlayer player = BasePlayer.FindByID(entry); GetSendMSG(player, message); }
                catch { }
        }

        private void BroadcastOnScreenFaction(string message, ushort faction = 0, ushort faction2 = 0)
        {
            if (faction == 0)
            {
                if (fdata.Factions.ContainsKey(faction2))
                    foreach (var player in BasePlayer.activePlayerList.Where(k => GetFactionPlayer(k) != null && GetFactionPlayer(k).Faction != faction2))
                        OnScreen(player, message, fdata.Factions[faction].UIColor);
                else
                    foreach (var player in BasePlayer.activePlayerList.Where(k => GetFactionPlayer(k) != null))
                        OnScreen(player, message);
            }
            else if (fdata.Factions.ContainsKey(faction))
                foreach (var entry in fdata.Factions[faction].factionPlayers)
                {
                    try { BasePlayer player = BasePlayer.FindByID(entry); OnScreen(player, message, fdata.Factions[faction].UIColor); }
                    catch { }
                }
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
            if (messages.ContainsKey(message))
                return string.Format(lang.GetMessage(message, this, p), arg1, arg2, arg3);
            else return message;
        }

        private void QuitFactionCreation(BasePlayer player)
        {
            if (FactionDetails[player.userID].creating)
            {
                FactionDetails.Remove(player.userID);
                GetSendMSG(player, "QuitFactionCreation");
            }
            else if (FactionEditor[player.userID].editing)
            {
                FactionEditor.Remove(player.userID);
                GetSendMSG(player, "QuitFactionEditing");
            }
            DestroyUI(player);
        }

        private void RemoveFaction(BasePlayer player, ushort ID, bool admin = false)
        {
            var factionname = fdata.Factions[ID].Name;
            fdata.Factions.Remove(ID);
            if (player != null)
            {
                DestroyUI(player);
                Broadcast($"{factionname} {GetMSG("FactionDeleted")}");
            }
            if (admin)
                ReassignPlayers(ID);
            SaveData();
        }

        private void ReassignPlayers(ushort ID)
        {
            foreach (var entry in fdata.Factions[ID].factionPlayers)
            {
                fdata.Players[entry].faction = default(ushort);
                try
                {
                    BasePlayer player = BasePlayer.FindByID(entry);
                    GetSendMSG(player, "FactionDeleted");
                    GetFactionPlayer(player).Panel = "Selection";
                    FactionPanel(player);
                }
                catch { }
            }
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


        private void DestroyUI(BasePlayer player, bool all = false)
        {
            FactionPlayer fp = GetFactionPlayer(player);
            if (fp != null)
            {
                fp.open = false;
                fp.SelectedFaction = new ushort();
                fp.TargetPlayer = new ulong();
                fp.Panel = "HOME";
            }
            if (all) CuiHelper.DestroyUi(player, PanelPlayer);
            CuiHelper.DestroyUi(player, PanelFAnnouncements);
            CuiHelper.DestroyUi(player, PanelStatic);
            CuiHelper.DestroyUi(player, PanelProfile);
            CuiHelper.DestroyUi(player, PanelFactions);
            CuiHelper.DestroyUi(player, PanelOnScreen);
            if (Professions)
                player.SendConsoleCommand("UI_ToggleProfessionsMenu close");
            if (CustomSets)
                player.SendConsoleCommand("ToggleCSUI close");
        }

        #endregion

        #region UI Creation
        private string PanelOnScreen = "OnScreen";
        private string PanelFactions = "FactionsPanel";
        private string PanelPlayer = "PlayerPanel";
        private string PanelStatic = "PanelStatic";
        private string PanelFAnnouncements = "PanelFAnnouncements";
        private string PanelProfile = "PanelProfile";
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
            {"Neutral", "0.9 0.9 0.0 1.0" },
            {"Spectator",  "0.9 0.9 0.0 1.0" },
            {"lightblue", "0.6 0.86 1.0 1.0" },
            {"buttonbg", "0.2 0.2 0.2 0.7" },
            {"buttongreen", "0.133 0.965 0.133 0.9" },
            {"buttonred", "0.964 0.133 0.133 0.9" },
            {"buttongrey", "0.8 0.8 0.8 0.9" },
            {"CSorange", "1.0 0.64 0.10 1.0" }
        };

        private Dictionary<string, string> ChatColor = new Dictionary<string, string>
        {
            {"Blue", "<color=#3366ff>" },
            {"Red", "<color=#e60000>" },
            {"Green", "<color=#29a329>" },
            {"Spectator", "<color=#ffff00>"}
        };

        #endregion

        #region UI Panels

        [ConsoleCommand("UI_FC_ToggleMenu")]
        private void cmdUI_FC_ToggleMenu(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null) return;
            if (configData.DisableMenu && !isAuth(player)) return;
            FactionPlayer fp = GetFactionPlayer(player);
            if (fp == null) { InitializeFactionPlayer(player); return; }
            if (arg.Args != null && arg.Args.Length > 0 && arg.Args[0] == "close")
            {
                DestroyUI(player);
                return;
            }
            if (!fp.open)
            {
                fp.open = true;
                ToggleFCMenu(player);
            }
            else
                DestroyUI(player);
        }

        private void ToggleFCMenu(BasePlayer player)
        {
            FactionPlayer fp = GetFactionPlayer(player);
            if (fp == null) { InitializeFactionPlayer(player); return; }
            fp.open = true;
            FCBackground(player);
        }

        void FCBackground(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, PanelStatic);
            var element = UI.CreateOverlayContainer(PanelStatic, "0 0 0 0", "0.05 .1", ".95 1", true);
            UI.LoadImage(ref element, PanelStatic, TryForImage("MainMenu"), "0 0", "1 1");
            CuiHelper.AddUi(player, element);
            FactionPanel(player);
            PlayerProfilePanel(player);
            FactionAnnouncementPanel(player);
        }


        private void FactionCreation(BasePlayer player, int step = 0)
        {
            FactionDesigner creation = null;
            if (FactionDetails.ContainsKey(player.userID))
                creation = FactionDetails[player.userID];
            var i = 0;
            Vector2 min = new Vector2(0f, 0f);
            Vector2 dimension = new Vector2(.2f, .15f);
            Vector2 offset2 = new Vector2(0.002f, 0.003f);
            var element = UI.CreateElementContainer(PanelFactions, UIColors["dark"], "0.3 0.3", "0.7 0.9");
            UI.CreatePanel(ref element, PanelFactions, UIColors["light"], "0.01 0.02", "0.99 0.98");
            switch (step)
            {
                case 0:
                    CuiHelper.DestroyUi(player, PanelFactions);
                    UI.CreatePanel(ref element, PanelFactions, "0 0 0 0", $".0001 0.0001", $"0.0002 0.0002", true);
                    //UI.CreateLabel(ref element, PanelFactions, UIColors["black"], GetMSG("FactionColor", TextColors["limegreen"], creation.faction.Name), 20, "0.05 0", ".95 1", TextAnchor.MiddleCenter);
                    List<string> UnavailableEmbleems = new List<string>();
                    UnavailableEmbleems = fdata.Factions.Select(k => k.Value.embleem).ToList();
                    foreach (var entry in configData.FactionEmblems_URLS.Where(k => !UnavailableEmbleems.Contains("Embleem" + k.Key)))
                    {
                        var pos = CalcButtonPos(i);
                        UI.LoadImage(ref element, PanelFactions, TryForImage("Embleem" + entry.Key), $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}");
                        UI.CreateButton(ref element, PanelFactions, "0 0 0 0", "", 12, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"UI_SelectEmbleem {entry.Key}"); i++;
                        if (i == 30) break;
                    }
                    break;
                case 1:
                    CuiHelper.DestroyUi(player, PanelFactions);
                    UI.CreateLabel(ref element, PanelFactions, UIColors["black"], GetMSG("FactionBegin"), 20, "0.05 0", ".95 1", TextAnchor.MiddleCenter);
                    break;
                case 2:
                    CuiHelper.DestroyUi(player, PanelFactions);
                    UI.CreateLabel(ref element, PanelFactions, UIColors["black"], GetMSG("FactionTag", player, creation.faction.Name), 20, "0.05 0", ".95 1", TextAnchor.MiddleCenter);
                    break;
                case 3:
                    CuiHelper.DestroyUi(player, PanelFactions);
                    UI.CreatePanel(ref element, PanelFactions, "0 0 0 0", $".0001 0.0001", $"0.0002 0.0002", true);
                    UI.CreateTextOutline(ref element, PanelFactions, UIColors["black"], UIColors["white"], GetMSG("FactionColor", player), 20, "0.05 1", ".95 1.1");
                    List<string> UnavailableColors = new List<string>();
                    UnavailableColors = fdata.Factions.Select(k => k.Value.ChatColor).ToList();
                    foreach (var entry in configData.Colors.Where(k => !UnavailableColors.Contains(k)))
                    {
                        var pos = CalcButtonPos(i);
                        UI.CreateButton(ref element, PanelFactions, HexTOUIColor(entry), "", 12, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"UI_SelectColor {entry}"); i++;
                    }
                    break;
                case 4:
                    if (!Kits && !CustomSets) { FactionCreation(player, 5); return; }
                    CuiHelper.DestroyUi(player, PanelFactions);
                    UI.CreatePanel(ref element, PanelFactions, "0 0 0 0", $".0001 0.0001", $"0.0002 0.0002", true);
                    UI.CreateTextOutline(ref element, PanelFactions, UIColors["black"], UIColors["white"], GetMSG("FactionKit", player), 20, "0.05 1", ".95 1.1");
                    List<string> UnavailableKits = new List<string>();
                    UnavailableKits = fdata.Factions.Select(k => k.Value.KitorSet).ToList();
                    foreach (var entry in configData.Kits_and_CustomSets.Where(k => !UnavailableKits.Contains(k)))
                    {
                        object isKit = Kits?.Call("isKit", new object[] { entry });
                        if ((!(isKit is bool) || !(bool)isKit) && !(bool)CustomSets?.Call("isSet", entry)) continue;
                        var pos = CalcButtonPos(i);
                        UI.CreateButton(ref element, PanelFactions, UIColors["black"], entry, 12, $"{pos[0]} {pos[1] + .075f}", $"{pos[2]} {pos[3]}", $"UI_SelectKit {entry}");
                        UI.CreateButton(ref element, PanelFactions, UIColors["black"], "ViewKit", 12, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[1] + .065f}", $"UI_ViewKit {entry}");
                        i++;
                    }
                    if (i == 0)
                    {
                        creation.stepNum = 3;
                        FactionCreation(player, 5);
                        return;
                    }
                    break;
                case 5:
                    CuiHelper.DestroyUi(player, PanelFactions);
                    UI.CreateLabel(ref element, PanelFactions, creation.faction.UIColor, GetMSG("FactionDescription", player, creation.faction.Name), 20, "0.05 0", ".95 1", TextAnchor.MiddleCenter);
                    break;
                case 6:
                    CuiHelper.DestroyUi(player, PanelFactions);
                    UI.CreatePanel(ref element, PanelFactions, "0 0 0 0", $".0001 0.0001", $"0.0002 0.0002", true);
                    UI.CreateLabel(ref element, PanelFactions, creation.faction.UIColor, GetMSG("CurrentDescription", player, creation.faction.description), 20, "0.05 0", ".95 1", TextAnchor.MiddleCenter);
                    UI.LoadImage(ref element, PanelFactions, TryForImage("LightButton"), "0.2 0.05", "0.45 0.15");
                    UI.CreateLabel(ref element, PanelFactions, UIColors["black"], GetMSG("SaveDescription", player), 18, "0.2 0.05", "0.45 0.15");
                    UI.CreateButton(ref element, PanelFactions, "0 0 0 0", "", 18, "0.2 0.05", "0.45 0.15", $"UI_Description save");
                    UI.LoadImage(ref element, PanelFactions, TryForImage("DarkButton"), "0.55 0.05", "0.8 0.15");
                    UI.CreateLabel(ref element, PanelFactions, UIColors["black"], GetMSG("Continue"), 18, "0.55 0.05", "0.8 0.15");
                    UI.CreateButton(ref element, PanelFactions, "0 0 0 0", "", 18, "0.55 0.05", "0.8 0.15", $"UI_Description continue");
                    break;
                case 7:
                    CuiHelper.DestroyUi(player, PanelFactions);
                    UI.CreateLabel(ref element, PanelFactions, creation.faction.UIColor, GetMSG("CurrentDescription", player, creation.faction.description), 20, "0.05 0", ".95 1", TextAnchor.MiddleCenter);
                    break;
                default:
                    CuiHelper.DestroyUi(player, PanelFactions);
                    element = UI.CreateElementContainer(PanelFactions, UIColors["dark"], "0.3 0.3", "0.7 0.9", true);
                    UI.CreatePanel(ref element, PanelFactions, UIColors["light"], "0.01 0.02", "0.99 0.98");
                    UI.CreateLabel(ref element, PanelFactions, creation.faction.UIColor, GetMSG("FactionDetails", player), 20, "0 .9", "1 1");
                    UI.CreateLabel(ref element, PanelFactions, creation.faction.UIColor, GetMSG("CreationDetails", player, creation.faction.Name, creation.faction.group), 20, "0.1 0.7", "0.9 0.89", TextAnchor.MiddleLeft);
                    UI.CreateLabel(ref element, PanelFactions, creation.faction.UIColor, creation.faction.description, 20, "0.1 0.1", "0.9 0.65", TextAnchor.UpperLeft);
                    UI.LoadImage(ref element, PanelFactions, TryForImage("LightButton"), "0.2 0.05", "0.45 0.15");
                    UI.CreateLabel(ref element, PanelFactions, UIColors["black"], GetMSG("SaveFaction", player), 18, "0.2 0.05", "0.45 0.15");
                    UI.CreateButton(ref element, PanelFactions, "0 0 0 0", "", 18, "0.2 0.05", "0.45 0.15", $"UI_SaveFaction", TextAnchor.MiddleCenter);
                    UI.LoadImage(ref element, PanelFactions, TryForImage("DarkButton"), "0.55 0.05", "0.8 0.15");
                    UI.CreateLabel(ref element, PanelFactions, UIColors["black"], GetMSG("Cancel", player), 18, "0.55 0.05", "0.8 0.15");
                    UI.CreateButton(ref element, PanelFactions, "0 0 0 0", "", 18, "0.55 0.05", "0.8 0.15", $"UI_ExitFactionCreation");
                    break;
            }
            CuiHelper.AddUi(player, element);
        }

        [ConsoleCommand("UI_ToggleFactionChat")]
        private void cmdUI_ToggleFactionChat(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            if (GetFactionPlayer(player).Faction == 0)
            {
                GetSendMSG(player, "NotInAFaction");
                return;
            }
            if (InFactionChat.Contains(player.userID))
            {
                InFactionChat.Remove(player.userID);
                GetSendMSG(player, "ExitFactionChat");
            }
            else
            {
                if (MakingFactionAnnouncement.Contains(player.userID))
                    MakingFactionAnnouncement.Remove(player.userID);
                InFactionChat.Add(player.userID);
                GetSendMSG(player, "EnterFactionChat");
            }
            PlayerPanel(player);
        }

        [ConsoleCommand("UI_ToggleFactionAnnouncement")]
        private void cmdUI_ToggleFactionAnnouncement(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            if (GetFactionPlayer(player).Faction == 0)
            {
                GetSendMSG(player, "NotInAFaction");
                return;
            }
            if (fdata.Factions[GetFactionPlayer(player).Faction].Leader != player.userID) return;
            if (MakingFactionAnnouncement.Contains(player.userID))
            {
                MakingFactionAnnouncement.Remove(player.userID);
                GetSendMSG(player, "ExitFactionAnnouncement");
            }
            else
            {
                if (InFactionChat.Contains(player.userID))
                    InFactionChat.Remove(player.userID);
                MakingFactionAnnouncement.Add(player.userID);
                GetSendMSG(player, "EnterFactionAnnouncement");
            }
            PlayerPanel(player);
        }

        private void ShadeZone(BasePlayer player, string zoneID)
        {
            if (ZoneDomes)
                ZoneDomes.Call("AddNewDome", player, zoneID);
        }

        private void UnShadeZone(BasePlayer player, string zoneID)
        {
            if (ZoneDomes)
                ZoneDomes.Call("RemoveExistingDome", null, zoneID);
        }

        private object GetZoneLocation(string zoneid) => ZoneManager?.Call("GetZoneLocation", zoneid);
        private object VerifyZoneID(string zoneid) => ZoneManager?.Call("CheckZoneID", zoneid);

        void OnScreen(BasePlayer player, string msg, string color = "0 0 0 1", string arg1 = "", string arg2 = "", string arg3 = "")
        {
            if (timers.ContainsKey(player.userID.ToString()))
            {
                timers[player.userID.ToString()].Destroy();
                timers.Remove(player.userID.ToString());
            }
            CuiHelper.DestroyUi(player, PanelOnScreen);
            var element = UI.CreateOverlayContainer(PanelOnScreen, "0.0 0.0 0.0 0.0", "0.55 0.8", "0.95 0.95");
            UI.CreateTextOutline(ref element, PanelOnScreen, UIColors["white"], color, GetMSG(msg, player, arg1, arg2, arg3), 24, "0.0 0.0", "1.0 1.0");
            CuiHelper.AddUi(player, element);
            timers.Add(player.userID.ToString(), timer.Once(6, () => CuiHelper.DestroyUi(player, PanelOnScreen)));
        }

        void PlayerPanel(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, PanelPlayer);
            if (!configData.Use_FactionAnnouncements && !configData.Use_PrivateFactionChat || GetFactionPlayer(player) == null) return;
            var element = UI.CreateElementContainer(PanelPlayer, "0 0 0 0", "0.01 0.02", "0.1 0.08");
            var faction = GetFactionPlayer(player).Faction;
            if (faction != 0)
            {
                if (configData.Use_PrivateFactionChat)
                {
                    if (InFactionChat.Contains(player.userID))
                        UI.LoadImage(ref element, PanelPlayer, TryForImage("ChatOn"), "0 0", ".48 1");
                    else UI.LoadImage(ref element, PanelPlayer, TryForImage("ChatOff"), "0 0", ".48 1");
                    UI.CreateButton(ref element, PanelPlayer, "0 0 0 0", "", 14, "0 0", ".48 1", $"UI_ToggleFactionChat", TextAnchor.MiddleCenter);
                }
                if (configData.Use_FactionAnnouncements && (isOwner(player, faction) || isModerator(player, faction)))
                {
                    if (MakingFactionAnnouncement.Contains(player.userID))
                        UI.LoadImage(ref element, PanelPlayer, TryForImage("AnnouncementOn"), ".52 0", "1 1");
                    else UI.LoadImage(ref element, PanelPlayer, TryForImage("AnnouncementOff"), ".52 0", "1 1");
                    UI.CreateButton(ref element, PanelPlayer, "0 0 0 0", "", 14, ".52 0", "1 1", $"UI_ToggleFactionAnnouncement", TextAnchor.MiddleCenter);
                }
            }
            CuiHelper.AddUi(player, element);
        }


        [ConsoleCommand("UI_SafeZone")]
        private void cmdUI_SafeZone(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null || arg.Args == null || arg.Args.Count() < 1) return;
            var zoneID = GetFactionPlayer(player).Faction.ToString();
            var factionname = fdata.Factions[GetFactionPlayer(player).Faction].Name;
            if (arg.Args[0] == "create")
            {
                string msg = $"{GetMSG("EnterFactionSafeZone", player, factionname)}";
                if (!AllowedToBuild(player)) { OnScreen(player, "SafeZoneWhereTCIS"); return; }
                Vector3 pos = player.transform.localPosition;
                if (!BuildingCheck(player, pos)) { OnScreen(player, "NoSafeZonesNearMonuments"); return; }
                List<string> build = new List<string>();
                build.Add("enter_message");
                build.Add(msg.ToString());
                build.Add("radius");
                build.Add(configData.SafeZones_Radius.ToString());
                string[] zoneArgs = build.ToArray();
                ZoneManager?.Call("CreateOrUpdateZone", zoneID, zoneArgs, pos);
                foreach (var entry in fdata.Factions[GetFactionPlayer(player).Faction].factionPlayers)
                    if (BasePlayer.activePlayerList.Contains(BasePlayer.FindByID(entry)))
                        ZoneManager?.Call("AddPlayerToZoneWhitelist", zoneID, BasePlayer.FindByID(entry));
                ZoneManager?.Call("AddPlayerToZoneWhitelist", zoneID, player);
                build.Clear();
                build.Add("eject");
                build.Add("true");
                zoneArgs = build.ToArray();
                ZoneManager?.Call("CreateOrUpdateZone", zoneID, zoneArgs, pos);
                ShadeZone(player, zoneID);
                FactionPanel(player);
            }
            else if (arg.Args[0] == "delete")
            {
                ZoneManager.Call("EraseZone", zoneID);
                UnShadeZone(player, zoneID);
                timer.Once(1, () => FactionPanel(player));
            }
        }

        private bool BuildingCheck(BasePlayer player, Vector3 pos)
        {
            foreach (var entry in MonumentLocations)
            {
                var distance = Vector3.Distance(pos, entry.position);
                if (distance < entry.radius + configData.SafeZones_Radius)
                    return false;
            }
            return true;
        }

        [ConsoleCommand("RunConsoleCommand")]
        private void cmdRunConsoleCommand(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null || arg.Args == null || arg.Args.Count() < 1) return;
            player.SendConsoleCommand(arg.Args[0]);
        }



        private void ManageFactionMenu(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, PanelFactions);
            var i = 0;
            var element = UI.CreateElementContainer(PanelFactions, UIColors["dark"], "0.35 0.3", "0.65 0.7", true);
            UI.CreatePanel(ref element, PanelFactions, UIColors["light"], "0.01 0.02", "0.99 0.98", true);
            UI.CreateLabel(ref element, PanelFactions, UIColors["header"], GetMSG("AdminMenu"), 100, "0.01 0.01", "0.99 0.99", TextAnchor.MiddleCenter);
            if (configData.FactionLimit == 0 || configData.FactionLimit > fdata.Factions.Count())
            { CreateOptionButton(ref element, PanelFactions, "GreenSquareButton", GetMSG("CreateFaction"), $"UI_NewFaction", 0); i++; }
            foreach (var entry in fdata.Factions)
            {
                var pos = CalcButtonPos(i);
                UI.CreateButton(ref element, PanelFactions, entry.Value.UIColor, GetMSG("DeleteFaction", player, entry.Value.Name), 12, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"UI_DeleteFaction {entry.Key}"); i++;
            }
            CuiHelper.AddUi(player, element);
        }

        private void ConfirmFactionDeletion(BasePlayer player, ushort ID)
        {
            CuiHelper.DestroyUi(player, PanelFactions);
            var FactionName = fdata.Factions[ID].Name;
            var ConfirmDelete = UI.CreateElementContainer(PanelFactions, UIColors["dark"], "0.2 0.4", "0.8 0.8", true);
            UI.CreatePanel(ref ConfirmDelete, PanelFactions, UIColors["light"], "0.01 0.02", "0.99 0.98");
            UI.CreateTextOutline(ref ConfirmDelete, PanelFactions, UIColors["white"], UIColors["black"], GetMSG("ConfirmDelete", player, FactionName), 20, "0.1 0.6", "0.9 0.9");
            UI.CreateButton(ref ConfirmDelete, PanelFactions, UIColors["buttonbg"], "Yes", 18, "0.2 0.2", "0.4 0.3", $"UI_DeleteFaction yes {ID}");
            UI.CreateButton(ref ConfirmDelete, PanelFactions, UIColors["buttonbg"], "No", 18, "0.6 0.2", "0.8 0.3", $"UI_DeleteFaction reject");
            CuiHelper.AddUi(player, ConfirmDelete);
        }

        void FactionAnnouncementPanel(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, PanelFAnnouncements);
            if (!configData.Use_FactionAnnouncements) return;
            FactionPlayer fp = GetFactionPlayer(player);
            var faction = fp.Faction;
            int i = -1;
            int n = 0;
            if (faction != 0)
            {
                int StartingEntry = fdata.Factions[fp.Faction].FactionAnnouncements.Keys.Max() - 4;
                if (StartingEntry < 0) StartingEntry = 0;
                int LastEntry = StartingEntry + 4;
                CuiElementContainer element = UI.CreateOverlayContainer(PanelFAnnouncements, "0 0 0 0", "0.25 .15", "0.76 0.35");
                UI.CreateTextOutline(ref element, PanelFAnnouncements, UIColors["red"], UIColors["white"], GetMSG("FactionAnnouncements", player), 22, "0 1.05", "1 1.4", TextAnchor.LowerCenter);
                foreach (var entry in fdata.Factions[fp.Faction].FactionAnnouncements.OrderBy(k => k.Key))
                {
                    i++;
                    if (i < StartingEntry) continue;
                    if (i >= StartingEntry)
                    {
                        var pos = ItemListPos(n);
                        UI.CreateLabel(ref element, PanelFAnnouncements, UIColors["white"], entry.Value, 20, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}");
                        n++;
                        if (i == LastEntry) break;
                    }
                }
                CuiHelper.AddUi(player, element);
            }
        }

        private float[] ItemListPos(int number)
        {
            Vector2 position = new Vector2(0f, 0.82f);
            Vector2 dimensions = new Vector2(1f, .2f);
            float offsetY = 0;
            float offsetX = 0;
            offsetY = ((-0.01f - dimensions.y) * number);
            Vector2 offset = new Vector2(offsetX, offsetY);
            Vector2 posMin = position + offset;
            Vector2 posMax = posMin + dimensions;
            return new float[] { posMin.x, posMin.y, posMax.x, posMax.y };
        }

        private class Games
        {
            [JsonProperty("response")]
            public Content Response;

            public class Content
            {
                [JsonProperty("game_count")]
                public int GameCount;
                [JsonProperty("games")]
                public Game[] Games;

                public class Game
                {
                    [JsonProperty("appid")]
                    public uint AppId;
                    [JsonProperty("playtime_2weeks")]
                    public int PlaytimeTwoWeeks;
                    [JsonProperty("playtime_forever")]
                    public int PlaytimeForever;
                }
            }
        }

        private class Summaries
        {
            [JsonProperty("response")]
            public Content Response;

            public class Content
            {
                [JsonProperty("players")]
                public Player[] Players;

                public class Player
                {
                    [JsonProperty("communityvisibilitystate")]
                    public int CommunityVisibilityState;
                }
            }
        }
        private int GetCommunityVisibilityState(Summaries s) => s.Response.Players[0].CommunityVisibilityState;
        private T Deserialise<T>(string json) => JsonConvert.DeserializeObject<T>(json);

        private void PrivateCheck(ulong ID)
        {
            webrequest.EnqueueGet(string.Format("http://api.steampowered.com/" + "ISteamUser/GetPlayerSummaries/v0002/?key={0}&steamids={1}", "4A395E77EEBB9087580BCC3F967D783E", ID), (code, response) =>
            {
                if (code == 200)
                {
                    var summaries = Deserialise<Summaries>(response);
                    if (GetCommunityVisibilityState(summaries) < 3)
                    {
                        Puts($"{GetDisplayName(ID)} has a private profile.");
                    }
                    else GetRustTime(ID);
                }
            }, this);
        }

        private void GetRustTime(ulong ID)
        {
            webrequest.EnqueueGet(string.Format("http://api.steampowered.com/" + "IPlayerService/GetOwnedGames/v0001/?key={0}&steamid={1}", "4A395E77EEBB9087580BCC3F967D783E", ID), (code, response) =>
            {
                if (code == 200)
                {

                    var games = Deserialise<Games>(response);
                    if (games != null)
                        foreach (var entry in games.Response.Games)
                        {
                            fdata.Players[ID].RustLifeTime = games.Response.Games.Where(k => k.AppId == 252490).Select(k => k.PlaytimeForever).FirstOrDefault() / 60;
                            fdata.Players[ID].RustTwoWeekTime = games.Response.Games.Where(k => k.AppId == 252490).Select(k => k.PlaytimeTwoWeeks).FirstOrDefault() / 60;
                        }
                }
            }, this);
        }

        void PlayerProfilePanel(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, PanelProfile);
            FactionPlayer fp = GetFactionPlayer(player);
            var page = fp.page;
            var panel = fp.Panel;
            CuiElementContainer element = UI.CreateOverlayContainer(PanelProfile, "0 0 0 0", "0.065 .11", ".19 .35");
            ///CREATE USER PROFILE INFO
            UI.LoadImage(ref element, PanelProfile, TryForImage(player.UserIDString, 0), "0 .65", ".35 1");
            UI.CreateLabel(ref element, PanelProfile, UIColors["white"], player.displayName, 16, ".37 .67", "1 1", TextAnchor.LowerLeft);
            UI.LoadImage(ref element, PanelProfile, TryForImage("hourglass"), "0.05 .38", ".3 .62");
            UI.CreateLabel(ref element, PanelProfile, UIColors["white"], GetMSG("PlayTimeMinutes", player, fdata.Players[player.userID].RustLifeTime.ToString(), fdata.Players[player.userID].RustTwoWeekTime.ToString()), 10, ".37 .35", "1 .65", TextAnchor.MiddleLeft);

            if (fp.Faction != 0)
            {
                var faction = fdata.Factions[fp.Faction];
                UI.CreateTextOutline(ref element, PanelProfile, faction.UIColor, UIColors["white"], faction.Name, 16, ".37 0", "1 .35", TextAnchor.MiddleLeft);
                if (!string.IsNullOrEmpty(faction.embleem))
                    UI.LoadImage(ref element, PanelProfile, TryForImage(faction.embleem), "-0.03 0.03", ".33 .3");
            }
            CuiHelper.AddUi(player, element);
        }

        static float mapSize = TerrainMeta.Size.x;
        static float GetPosition(float pos) => (pos + mapSize / 2f) / mapSize;

        void FactionPanel(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, PanelFactions);
            CuiHelper.DestroyUi(player, PanelOnScreen);
            FactionPlayer fp = GetFactionPlayer(player);
            var page = fp.page;
            var panel = fp.Panel;
            var faction = fp.Faction;
            int entriesallowed;
            int remainingentries;
            int count;
            int shownentries;
            int i;
            int n;
            float[] pos;
            CuiElementContainer element = UI.CreateOverlayContainer(PanelFactions, "0 0 0 0", "0.05 .41", ".95 1");
            UI.CreateButton(ref element, PanelFactions, UIColors["red"], "X", 14, "0 .95", ".025 1", $"UI_FC_ToggleMenu close", TextAnchor.MiddleCenter);
            ////LEFT PANEL
            //UI.CreatePanel(ref element, PanelFactions, UIColors["white"], "0.135 0", ".305 .8");

            ////MIDDLE PANEL
            //UI.CreatePanel(ref element, PanelFactions, UIColors["white"], "0.315 0", ".685 .8");

            ////RIGHT PANEL
            //UI.CreatePanel(ref element, PanelFactions, UIColors["white"], "0.695 0", ".865 .8");

            ////
            UI.CreateTextOutline(ref element, PanelFactions, UIColors["white"], UIColors["black"], GetMSG("HOME"), 16, "0.22 0.89", "0.31 .99");
            UI.CreateTextOutline(ref element, PanelFactions, UIColors["white"], UIColors["black"], GetMSG("MAP"), 16, "0.314 0.89", "0.404 .99");
            UI.CreateTextOutline(ref element, PanelFactions, UIColors["white"], UIColors["black"], GetMSG("Faction"), 16, "0.408 0.89", "0.498 .99");
            UI.CreateTextOutline(ref element, PanelFactions, UIColors["white"], UIColors["black"], GetMSG("AllPlayers"), 16, "0.502 0.89", "0.592 .99");
            UI.CreateTextOutline(ref element, PanelFactions, UIColors["white"], UIColors["black"], GetMSG("ScoreBoard"), 16, "0.596 0.89", "0.686 .99");
            UI.CreateTextOutline(ref element, PanelFactions, UIColors["white"], UIColors["black"], GetMSG("Options"), 16, "0.69 0.89", "0.78 .99");
            if (panel != "HOME")
                UI.CreateButton(ref element, PanelFactions, "0 0 0 0", "", 18, "0.22 0.89", "0.31 .99", $"UI_FC_ChangePanel HOME");
            UI.CreateButton(ref element, PanelFactions, "0 0 0 0", "", 18, "0.314 0.89", "0.404 .99", $"UI_FC_ChangePanel MAP");
            if (panel != "FLIST")
                UI.CreateButton(ref element, PanelFactions, "0 0 0 0", "", 18, "0.408 0.89", "0.498 .99", $"UI_FC_ChangePanel FLIST");
            if (panel != "PLIST" && (configData.Use_PlayerListMenu || isAuth(player)))
                UI.CreateButton(ref element, PanelFactions, "0 0 0 0", "", 18, "0.502 0.89", "0.592 .99", $"UI_FC_ChangePanel PLIST");
            if (panel != "SCOREBOARD")
                UI.CreateButton(ref element, PanelFactions, "0 0 0 0", "", 18, "0.596 0.89", "0.686 .99", $"UI_FC_ChangePanel SCOREBOARD");
            if (panel != "OPTIONS" && isAuth(player))
                UI.CreateButton(ref element, PanelFactions, "0 0 0 0", "", 18, "0.69 0.89", "0.78 .99", $"UI_FC_ChangePanel OPTIONS");
            switch (panel)
            {
                case "HOME":
                    if (!string.IsNullOrEmpty(configData.HomePageMessage))
                        UI.CreateTextOutline(ref element, PanelFactions, UIColors["white"], UIColors["black"], configData.HomePageMessage, 16, "0.32 0.02", ".68 .78");
                    i = 0;
                    foreach (var entry in fdata.Players[player.userID].PendingInvites.Where(k => fdata.Factions.ContainsKey(k)))
                    {
                        pos = LeftPanelPos(i);
                        UI.LoadImage(ref element, PanelFactions, TryForImage("LightButton"), $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}");
                        UI.CreateLabel(ref element, PanelFactions, fdata.Factions[entry].UIColor, GetMSG("FactionInvite", player, fdata.Factions[entry].Name), 14, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}");
                        UI.CreateButton(ref element, PanelFactions, "0 0 0 0", "", 14, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"UI_FC_ChangePanel HOME {entry}");
                        i++;
                        if (i == 7) break;
                    }
                    if (faction == 0)
                    {
                        i = 7;
                        pos = LeftPanelPos(i);
                        if (configData.Use_AllowPlayersToCreateFactions && (configData.FactionLimit == 0 || configData.FactionLimit > fdata.Factions.Count()))
                        {
                            UI.LoadImage(ref element, PanelFactions, TryForImage("LightButton"), $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}");
                            UI.CreateLabel(ref element, PanelFactions, UIColors["white"], GetMSG("CreateFaction"), 14, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}");
                            UI.CreateButton(ref element, PanelFactions, "0 0 0 0", "", 8, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"UI_NewFaction");
                            i++;
                        }
                        pos = LeftPanelPos(i);
                        UI.LoadImage(ref element, PanelFactions, TryForImage("DarkButton"), $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}");
                        UI.CreateLabel(ref element, PanelFactions, UIColors["white"], GetMSG("JoinFaction"), 14, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}");
                        UI.CreateButton(ref element, PanelFactions, "0 0 0 0", "", 14, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"UI_FC_ChangePanel Selection");
                    }
                    else
                    {
                        pos = LeftPanelPos(8);
                        UI.LoadImage(ref element, PanelFactions, TryForImage("DarkButton"), $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}");
                        UI.CreateLabel(ref element, PanelFactions, UIColors["white"], GetMSG("LeaveFaction"), 14, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}");
                        UI.CreateButton(ref element, PanelFactions, "0 0 0 0", "", 14, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"UI_LeaveFaction");
                    }
                    if (fp.SelectedFaction != new ushort())
                    {
                        //UI.CreatePanel(ref element, PanelFactions, UIColors["white"], "0.695 0", ".865 .8");
                        var f = fdata.Factions[fp.SelectedFaction];
                        UI.CreateTextOutline(ref element, PanelFactions, f.UIColor, UIColors["white"], f.Name, 18, "0.7 0.74", ".86 .78", TextAnchor.UpperCenter);
                        if (!string.IsNullOrEmpty(f.embleem))
                            UI.LoadImage(ref element, PanelFactions, TryForImage(f.embleem), "0.73 0.5", ".83 .74");
                        UI.CreateLabel(ref element, PanelFactions, f.UIColor, GetMSG("CreationLeader", player, fdata.Players.ContainsKey(f.Leader) ? GetDisplayName(f.Leader) : "NONE"), 14, "0.7 .44", ".86 .49");
                        UI.CreateLabel(ref element, PanelFactions, f.UIColor, GetMSG("CreationPlayerCount", player, f.factionPlayers.Count().ToString()), 14, "0.7 .39", ".86 .44");
                        UI.CreateLabel(ref element, PanelFactions, f.UIColor, f.description, 10, "0.7 .15", ".86 .39");
                        pos = RightPanelPos(8);
                        UI.LoadImage(ref element, PanelFactions, TryForImage("LightButton"), $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}");
                        UI.CreateLabel(ref element, PanelFactions, UIColors["white"], GetMSG("JoinFaction"), 14, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}");
                        UI.CreateButton(ref element, PanelFactions, "0 0 0 0", "", 8, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"UI_FactionSelection", TextAnchor.MiddleCenter);
                    }
                    break;
                case "MAP":
                    float minx;
                    float miny;
                    float maxx;
                    float maxy;
                    UI.LoadImage(ref element, PanelFactions, TryForImage("worldmap", 0), "0.315 0", ".685 .8");
                    foreach (var entry in fdata.Factions)
                    {
                        object bzone = ZoneManager?.Call("GetZoneLocation", entry.Key.ToString());
                        if (bzone is Vector3)
                        {
                            Vector3 safezone = (Vector3)bzone;
                            if (safezone == new Vector3()) continue;
                            minx = 0.315f + (0.37f * GetPosition(safezone.x)) - .02f;
                            miny = 0f + (0.8f * GetPosition(safezone.z)) - .03f;
                            maxx = minx + .04f;
                            maxy = miny + .06f;
                            if (!string.IsNullOrEmpty(fdata.Factions[entry.Key].embleem))
                                UI.LoadImage(ref element, PanelFactions, TryForImage(entry.Value.embleem), $"{minx} {miny}", $"{maxx} {maxy}");
                            else UI.LoadImage(ref element, PanelFactions, TryForImage("safezone"), $"{minx} {miny}", $"{maxx} {maxy}");
                        }
                    }
                    if (faction != 0)
                    {
                        if (configData.ShowFactionPlayersOnMap)
                            foreach (var entry in FactionPlayers.Where(k => k.Faction == faction && k.player.userID != player.userID))
                            {
                                minx = 0.315f + (0.37f * GetPosition(entry.player.transform.localPosition.x)) - .005f;
                                miny = 0f + (0.8f * GetPosition(entry.player.transform.localPosition.z)) - .007f;
                                maxx = minx + .01f;
                                maxy = miny + .014f;
                                UI.LoadImage(ref element, PanelFactions, TryForImage("Friend"), $"{minx} {miny}", $"{maxx} {maxy}");
                            }
                        if (FactionsTax)
                            if (isOwner(player, faction) || isModerator(player, faction))
                            {
                                var result = TaxboxLocation(faction);
                                if (result is Vector3)
                                {
                                    Vector3 box = (Vector3)result;
                                    minx = 0.315f + (0.37f * GetPosition(box.x)) - .007f;
                                    miny = 0f + (0.8f * GetPosition(box.z)) - .009f;
                                    maxx = minx + .014f;
                                    maxy = miny + .018f;
                                    UI.LoadImage(ref element, PanelFactions, TryForImage("box"), $"{minx} {miny}", $"{maxx} {maxy}");
                                }
                            }
                        if (LastFactionStanding)
                        {
                            var result = LFSLocation(faction);
                            if (result is Vector3)
                            {
                                Vector3 battle = (Vector3)result;
                                minx = 0.315f + (0.37f * GetPosition(battle.x)) - .03f;
                                miny = 0f + (0.8f * GetPosition(battle.z)) - .04f;
                                maxx = minx + .06f;
                                maxy = miny + .08f;
                                UI.LoadImage(ref element, PanelFactions, TryForImage("battle"), $"{minx} {miny}", $"{maxx} {maxy}");
                            }
                        }
                    }

                        minx = 0.315f + (0.37f * GetPosition(player.transform.localPosition.x)) - .005f;
                        miny = 0f + (0.8f * GetPosition(player.transform.localPosition.z)) - .007f;
                        maxx = minx + .01f;
                        maxy = miny + .014f;
                        UI.LoadImage(ref element, PanelFactions, TryForImage("Self"), $"{minx} {miny}", $"{maxx} {maxy}");
                    if (faction != 0)
                        if (isOwner(player, faction) || isModerator(player, faction))
                        {
                            i = 0;
                            pos = LeftPanelPos(i);
                            if (FactionsTax)
                            {
                                UI.LoadImage(ref element, PanelFactions, TryForImage("LightButton"), $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}");
                                UI.CreateLabel(ref element, PanelFactions, UIColors["white"], GetMSG("SetTaxBox"), 14, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}");
                                UI.CreateButton(ref element, PanelFactions, "0 0 0 0", "", 14, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"RunConsoleCommand UI_SetTaxBox");
                                i++;
                            }
                            if (configData.SafeZones_Allow && ZoneManager)
                            {
                                pos = LeftPanelPos(i);
                                object result = ZoneManager?.Call("AddPlayerToZoneWhitelist", GetFactionPlayer(player).Faction.ToString(), player);
                                if (result != null && result is bool && (bool)result)
                                {
                                    UI.LoadImage(ref element, PanelFactions, TryForImage("DarkButton"), $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}");
                                    UI.CreateLabel(ref element, PanelFactions, UIColors["white"], GetMSG("DeleteFactionSafeZone"), 14, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}");
                                    UI.CreateButton(ref element, PanelFactions, "0 0 0 0", "", 14, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"UI_SafeZone delete");
                                }
                                else
                                {
                                    UI.LoadImage(ref element, PanelFactions, TryForImage("LightButton"), $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}");
                                    UI.CreateLabel(ref element, PanelFactions, UIColors["white"], GetMSG("CreateFactionSafeZone"), 14, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}");
                                    UI.CreateButton(ref element, PanelFactions, "0 0 0 0", "", 14, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"UI_SafeZone create");
                                }
                            }
                        }
                    break;
                case "FLIST":
                    if (faction != 0)
                    {
                        var FactionData = fdata.Factions[faction];
                        if (!string.IsNullOrEmpty(FactionData.embleem))
                            UI.LoadImage(ref element, PanelFactions, TryForImage(FactionData.embleem), "0.37 .1", ".63 .7");
                        i = 0;
                        pos = LeftPanelPos(i);
                        UI.LoadImage(ref element, PanelFactions, TryForImage("LightButton"), $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}");
                        UI.CreateLabel(ref element, PanelFactions, UIColors["white"], GetMSG("ToggleOnlineOnly", player), 14, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}");
                        UI.CreateButton(ref element, PanelFactions, "0 0 0 0", "", 8, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"UI_ToggleOnlineOnly", TextAnchor.MiddleCenter);
                        List<ulong> FactionPlayers = new List<ulong>();
                        if (fp.OnlineOnly)
                        {
                            UI.CreateTextOutline(ref element, PanelFactions, FactionData.UIColor, UIColors["white"], GetMSG("OnlineOnly", player, FactionData.Name), 18, "0.315 0.8", ".685 .85", TextAnchor.UpperCenter);
                            FactionPlayers = BasePlayer.activePlayerList.Where(k => k.userID != player.userID && FactionData.factionPlayers.Contains(k.userID)).Select(k => k.userID).ToList();
                        }
                        else
                        {
                            UI.CreateTextOutline(ref element, PanelFactions, FactionData.UIColor, UIColors["white"], GetMSG("AllMembers", player, FactionData.Name), 18, "0.315 0.8", ".685 .85", TextAnchor.UpperCenter);
                            FactionPlayers = FactionData.factionPlayers.Where(k => k != player.userID).ToList();
                        }
                        count = FactionData.factionPlayers.Where(k => k != player.userID).Count();
                        entriesallowed = 38;
                        remainingentries = count - (page * entriesallowed);
                        if (page > 0)
                        {
                            UI.LoadImage(ref element, PanelFactions, TryForImage("BACK"), "0.56 0.03", "0.61 0.13");
                            UI.CreateButton(ref element, PanelFactions, "0 0 0 0", "", 18, "0.56 0.03", "0.61 0.13", $"UI_FC_TurnPage {page - 1}");
                        }
                        if (remainingentries > entriesallowed)
                        {

                            UI.LoadImage(ref element, PanelFactions, TryForImage("NEXT"), "0.62 0.03", "0.67 0.13");
                            UI.CreateButton(ref element, PanelFactions, "0 0 0 0", "", 18, "0.62 0.03", "0.67 0.13", $"UI_FC_TurnPage {page + 1}");
                        }
                        shownentries = page * entriesallowed;
                        i = 0;
                        n = 0;
                        foreach (var entry in FactionPlayers)
                        {
                            i++;
                            if (i < shownentries + 1) continue;
                            else if (i <= shownentries + entriesallowed)
                            {
                                pos = MiddlePanelPos(n);
                                var name = GetDisplayName(entry);
                                UI.LoadImage(ref element, PanelFactions, TryForImage("LightButton"), $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}");
                                UI.CreateLabel(ref element, PanelFactions, UIColors["white"], name, 12, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}");
                                UI.CreateButton(ref element, PanelFactions, "0 0 0 0", "", 12, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"UI_FC_ChangePanel FLIST {entry}");
                                n++;
                            }
                        }
                        if (fp.TargetPlayer != new ulong())
                        {
                            var Playerdata = fdata.Players[fp.TargetPlayer];
                            //UI.CreatePanel(ref element, PanelFactions, UIColors["white"], "0.695 0", ".865 .8");
                            UI.LoadImage(ref element, PanelFactions, TryForImage(fp.TargetPlayer.ToString(), 0), "0.73 0.55", ".83 .79");
                            UI.CreateLabel(ref element, PanelFactions, UIColors["white"], GetDisplayName(fp.TargetPlayer), 16, "0.7 0.49", ".86 .55", TextAnchor.UpperCenter);
                            UI.LoadImage(ref element, PanelFactions, TryForImage("hourglass"), "0.73 .41", ".76 .49");
                            UI.CreateLabel(ref element, PanelFactions, UIColors["white"], GetMSG("PlayTimeMinutes", player, Playerdata.RustLifeTime.ToString(), Playerdata.RustTwoWeekTime.ToString()), 10, "0.77 .39", ".86 .49", TextAnchor.MiddleLeft);
                            UI.CreateLabel(ref element, PanelFactions, UIColors["white"], BasePlayer.activePlayerList.Contains(BasePlayer.FindByID(fp.TargetPlayer)) ? GetMSG("ONLINE") : GetMSG("OFFLINE"), 16, "0.7 .34", ".86 .39");
                            if (isOwner(player, faction) || isModerator(player, faction))
                            {
                                if (FactionData.Leader == player.userID)
                                {
                                    i = 7;
                                    pos = RightPanelPos(i);
                                    if (!FactionData.Moderators.Contains(fp.TargetPlayer))
                                    {
                                        UI.LoadImage(ref element, PanelFactions, TryForImage("LightButton"), $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}");
                                        UI.CreateLabel(ref element, PanelFactions, UIColors["white"], GetMSG("AddModerator"), 14, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}");
                                        UI.CreateButton(ref element, PanelFactions, "0 0 0 0", "", 8, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"UI_FC_Moderator add", TextAnchor.MiddleCenter);
                                        i++;
                                    }
                                    else
                                    {
                                        UI.LoadImage(ref element, PanelFactions, TryForImage("DarkButton"), $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}");
                                        UI.CreateLabel(ref element, PanelFactions, UIColors["white"], GetMSG("RemoveModerator"), 14, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}");
                                        UI.CreateButton(ref element, PanelFactions, "0 0 0 0", "", 8, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"UI_FC_Moderator remove", TextAnchor.MiddleCenter);
                                        i++;
                                    }
                                }
                                i = 8;
                                pos = RightPanelPos(i);
                                if (fdata.Players[fp.TargetPlayer].faction == faction)
                                {
                                    UI.LoadImage(ref element, PanelFactions, TryForImage("DarkButton"), $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}");
                                    UI.CreateLabel(ref element, PanelFactions, UIColors["white"], GetMSG("KickFromFaction"), 14, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}");
                                    UI.CreateButton(ref element, PanelFactions, "0 0 0 0", "", 8, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"UI_FC_KickPlayer", TextAnchor.MiddleCenter);
                                }
                            }
                        }
                        if (FactionData.Leader == player.userID || FactionData.Moderators.Contains(player.userID))
                        {
                            i = 7;
                            pos = LeftPanelPos(i);
                            UI.LoadImage(ref element, PanelFactions, TryForImage("LightButton"), $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}");
                            UI.CreateLabel(ref element, PanelFactions, UIColors["white"], GetMSG("TogglePrivate", player, FactionData.Private.ToString()), 14, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}");
                            UI.CreateButton(ref element, PanelFactions, "0 0 0 0", "", 14, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"UI_FC_TogglePrivate");
                            i++;
                            pos = LeftPanelPos(i);
                            UI.LoadImage(ref element, PanelFactions, TryForImage("LightButton"), $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}");
                            UI.CreateLabel(ref element, PanelFactions, UIColors["white"], GetMSG("InvitePlayers", player), 14, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}");
                            UI.CreateButton(ref element, PanelFactions, "0 0 0 0", "", 14, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"UI_FC_ChangePanel Invite");
                            i++;

                        }
                        pos = LeftPanelPos(8);
                        UI.LoadImage(ref element, PanelFactions, TryForImage("DarkButton"), $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}");
                        UI.CreateLabel(ref element, PanelFactions, UIColors["white"], GetMSG("LeaveFaction"), 14, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}");
                        UI.CreateButton(ref element, PanelFactions, "0 0 0 0", "", 14, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"UI_LeaveFaction");
                    }
                    else
                    {
                        UI.CreateTextOutline(ref element, PanelFactions, UIColors["white"], UIColors["black"], GetMSG("NotInFaction", player), 36, "0.32 0.02", ".68 .78");
                        i = 7;
                        pos = LeftPanelPos(i);
                        if (configData.Use_AllowPlayersToCreateFactions && (configData.FactionLimit == 0 || configData.FactionLimit > fdata.Factions.Count()))
                        {
                            UI.LoadImage(ref element, PanelFactions, TryForImage("LightButton"), $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}");
                            UI.CreateLabel(ref element, PanelFactions, UIColors["white"], GetMSG("CreateFaction"), 14, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}");
                            UI.CreateButton(ref element, PanelFactions, "0 0 0 0", "", 8, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"UI_NewFaction");
                            i++;
                        }
                        pos = LeftPanelPos(i);
                        UI.LoadImage(ref element, PanelFactions, TryForImage("DarkButton"), $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}");
                        UI.CreateLabel(ref element, PanelFactions, UIColors["white"], GetMSG("JoinFaction"), 14, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}");
                        UI.CreateButton(ref element, PanelFactions, "0 0 0 0", "", 14, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"UI_FC_ChangePanel Selection");
                    }
                    break;
                case "Invite":
                    UI.CreateTextOutline(ref element, PanelFactions, UIColors["white"], UIColors["black"], GetMSG("MoreDetails", player), 18, "0.315 0.8", ".685 .85", TextAnchor.UpperCenter);
                    List<ulong> PlayerList = fdata.Players.Where(k => k.Value.faction == 0 && !k.Value.PendingInvites.Contains(faction)).Select(k => k.Key).ToList();
                    count = PlayerList.Count();
                    entriesallowed = 38;
                    remainingentries = count - (page * entriesallowed);
                    if (page > 0)
                    {
                        UI.LoadImage(ref element, PanelFactions, TryForImage("BACK"), "0.56 0.03", "0.61 0.13");
                        UI.CreateButton(ref element, PanelFactions, "0 0 0 0", "", 18, "0.56 0.03", "0.61 0.13", $"UI_FC_TurnPage {page - 1}");
                    }
                    if (remainingentries > entriesallowed)
                    {

                        UI.LoadImage(ref element, PanelFactions, TryForImage("NEXT"), "0.62 0.03", "0.67 0.13");
                        UI.CreateButton(ref element, PanelFactions, "0 0 0 0", "", 18, "0.62 0.03", "0.67 0.13", $"UI_FC_TurnPage {page + 1}");
                    }
                    shownentries = page * entriesallowed;
                    i = 0;
                    n = 0;
                    foreach (var entry in PlayerList)
                    {
                        i++;
                        if (i < shownentries + 1) continue;
                        else if (i <= shownentries + entriesallowed)
                        {
                            pos = MiddlePanelPos(n);
                            var name = GetDisplayName(entry);
                            UI.LoadImage(ref element, PanelFactions, TryForImage("LightButton"), $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}");
                            UI.CreateLabel(ref element, PanelFactions, UIColors["white"], name, 12, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}");
                            UI.CreateButton(ref element, PanelFactions, "0 0 0 0", "", 12, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"UI_FC_ChangePanel Invite {entry}");
                            n++;
                        }
                    }
                    if (fp.TargetPlayer != new ulong())
                    {
                        var Playerdata = fdata.Players[fp.TargetPlayer];
                        //UI.CreatePanel(ref element, PanelFactions, UIColors["white"], "0.695 0", ".865 .8");

                        UI.LoadImage(ref element, PanelFactions, TryForImage(fp.TargetPlayer.ToString(), 0), "0.73 0.55", ".83 .79");
                        UI.CreateLabel(ref element, PanelFactions, UIColors["white"], GetDisplayName(fp.TargetPlayer), 16, "0.7 0.49", ".86 .55", TextAnchor.UpperCenter);
                        UI.LoadImage(ref element, PanelFactions, TryForImage("hourglass"), "0.73 .41", ".76 .49");
                        UI.CreateLabel(ref element, PanelFactions, UIColors["white"], GetMSG("PlayTimeMinutes", player, Playerdata.RustLifeTime.ToString(), Playerdata.RustTwoWeekTime.ToString()), 10, "0.77 .39", ".86 .49", TextAnchor.MiddleLeft);
                        UI.CreateLabel(ref element, PanelFactions, UIColors["white"], BasePlayer.activePlayerList.Contains(BasePlayer.FindByID(fp.TargetPlayer)) ? GetMSG("ONLINE") : GetMSG("OFFLINE"), 16, "0.7 .34", ".86 .39");
                        if (isOwner(player, faction) || isModerator(player, faction))
                        {
                            if (fdata.Players[fp.TargetPlayer].faction == 0)
                            {
                                i = 8;
                                pos = RightPanelPos(i);
                                UI.LoadImage(ref element, PanelFactions, TryForImage("LightButton"), $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}");
                                UI.CreateLabel(ref element, PanelFactions, UIColors["black"], GetMSG("InviteToFaction"), 14, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}");
                                UI.CreateButton(ref element, PanelFactions, "0 0 0 0", "", 8, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"UI_FC_SendInvite", TextAnchor.MiddleCenter);
                            }
                        }
                    }
                    break;
                case "Selection":
                    if (fdata.Factions.Count() == 0) { GetSendMSG(player, "NoFactionsToJoin"); return; }
                    if (fp.Faction != 0)
                    {
                        GetSendMSG(player, "InAFaction");
                        return;
                    }
                    CuiHelper.DestroyUi(player, PanelFactions);
                    count = fdata.Factions.Count();
                    UI.CreateTextOutline(ref element, PanelFactions, UIColors["white"], UIColors["black"], GetMSG("FactionSelectionTitle", player), 18, "0.315 0.8", ".685 .85", TextAnchor.UpperCenter);
                    entriesallowed = 38;
                    remainingentries = count - (page * entriesallowed);
                    if (page > 0)
                    {
                        UI.LoadImage(ref element, PanelFactions, TryForImage("BACK"), "0.56 0.03", "0.61 0.13");
                        UI.CreateButton(ref element, PanelFactions, "0 0 0 0", "", 18, "0.56 0.03", "0.61 0.13", $"UI_FC_TurnPage {page - 1}");
                    }
                    if (remainingentries > entriesallowed)
                    {

                        UI.LoadImage(ref element, PanelFactions, TryForImage("NEXT"), "0.62 0.03", "0.67 0.13");
                        UI.CreateButton(ref element, PanelFactions, "0 0 0 0", "", 18, "0.62 0.03", "0.67 0.13", $"UI_FC_TurnPage {page + 1}");
                    }
                    shownentries = page * entriesallowed;
                    i = 0;
                    n = 0;
                    foreach (var entry in fdata.Factions.Where(k => !k.Value.BanList.Contains(player.userID) && !k.Value.Private))
                    {
                        i++;
                        if (i < shownentries + 1) continue;
                        else if (i <= shownentries + entriesallowed)
                        {
                            pos = MiddlePanelPos(n);
                            UI.CreateButton(ref element, PanelFactions, entry.Value.UIColor, entry.Value.Name, 12, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"UI_FactionInfo {entry.Key}"); n++;
                        }
                    }
                    if (fp.SelectedFaction != new ushort())
                    {
                        //UI.CreatePanel(ref element, PanelFactions, UIColors["white"], "0.695 0", ".865 .8");
                        var f = fdata.Factions[fp.SelectedFaction];
                        UI.CreateTextOutline(ref element, PanelFactions, f.UIColor, UIColors["white"], f.Name, 18, "0.7 0.74", ".86 .78", TextAnchor.UpperCenter);
                        if (!string.IsNullOrEmpty(f.embleem))
                            UI.LoadImage(ref element, PanelFactions, TryForImage(f.embleem), "0.73 0.5", ".83 .74");
                        UI.CreateLabel(ref element, PanelFactions, f.UIColor, GetMSG("CreationLeader", player, fdata.Players.ContainsKey(f.Leader) ? GetDisplayName(f.Leader) : "NONE"), 14, "0.7 .44", ".86 .49");
                        UI.CreateLabel(ref element, PanelFactions, f.UIColor, GetMSG("CreationPlayerCount", player, f.factionPlayers.Count().ToString()), 14, "0.7 .39", ".86 .44");
                        UI.CreateLabel(ref element, PanelFactions, f.UIColor, f.description, 10, "0.7 .15", ".86 .39");
                        pos = RightPanelPos(8);
                        UI.LoadImage(ref element, PanelFactions, TryForImage("LightButton"), $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}");
                        UI.CreateLabel(ref element, PanelFactions, UIColors["white"], GetMSG("JoinFaction"), 14, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}");
                        UI.CreateButton(ref element, PanelFactions, "0 0 0 0", "", 8, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"UI_FactionSelection", TextAnchor.MiddleCenter);
                    }
                    break;

                case "PLIST":
                    i = 0;
                    pos = LeftPanelPos(i);
                    UI.LoadImage(ref element, PanelFactions, TryForImage("LightButton"), $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}");
                    UI.CreateLabel(ref element, PanelFactions, UIColors["white"], GetMSG("ToggleOnlineOnly", player), 14, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}");
                    UI.CreateButton(ref element, PanelFactions, "0 0 0 0", "", 8, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"UI_ToggleOnlineOnly", TextAnchor.MiddleCenter);
                    List<ulong> AllPlayers = new List<ulong>();
                    if (fp.OnlineOnly)
                    {
                        UI.CreateTextOutline(ref element, PanelFactions, UIColors["white"], UIColors["black"], GetMSG("MoreDetails-OnlineOnly", player), 18, "0.315 0.8", ".685 .85", TextAnchor.UpperCenter);
                        AllPlayers = BasePlayer.activePlayerList.Where(k => k.userID != player.userID).Select(k => k.userID).ToList();
                    }
                    else
                    {
                        UI.CreateTextOutline(ref element, PanelFactions, UIColors["white"], UIColors["black"], GetMSG("MoreDetails", player), 18, "0.315 0.8", ".685 .85", TextAnchor.UpperCenter);
                        AllPlayers = fdata.Players.Where(k => k.Key != player.userID).Select(k => k.Key).ToList();
                    }
                    count = AllPlayers.Count();
                    entriesallowed = 38;
                    remainingentries = count - (page * entriesallowed);
                    if (page > 0)
                    {
                        UI.LoadImage(ref element, PanelFactions, TryForImage("BACK"), "0.56 0.03", "0.61 0.13");
                        UI.CreateButton(ref element, PanelFactions, "0 0 0 0", "", 18, "0.56 0.03", "0.61 0.13", $"UI_FC_TurnPage {page - 1}");
                    }
                    if (remainingentries > entriesallowed)
                    {

                        UI.LoadImage(ref element, PanelFactions, TryForImage("NEXT"), "0.62 0.03", "0.67 0.13");
                        UI.CreateButton(ref element, PanelFactions, "0 0 0 0", "", 18, "0.62 0.03", "0.67 0.13", $"UI_FC_TurnPage {page + 1}");
                    }
                    shownentries = page * entriesallowed;
                    i = 0;
                    n = 0;
                    foreach (var entry in AllPlayers)
                    {
                        i++;
                        if (i < shownentries + 1) continue;
                        else if (i <= shownentries + entriesallowed)
                        {
                            pos = MiddlePanelPos(n);
                            var name = GetDisplayName(entry);
                            UI.LoadImage(ref element, PanelFactions, TryForImage("LightButton"), $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}");
                            UI.CreateLabel(ref element, PanelFactions, UIColors["white"], name, 12, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}");
                            UI.CreateButton(ref element, PanelFactions, "0 0 0 0", "", 12, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"UI_FC_ChangePanel PLIST {entry}");
                            n++;
                        }
                    }
                    if (fp.TargetPlayer != new ulong())
                    {
                        var Playerdata = fdata.Players[fp.TargetPlayer];
                        //UI.CreatePanel(ref element, PanelFactions, UIColors["white"], "0.695 0", ".865 .8");

                        UI.LoadImage(ref element, PanelFactions, TryForImage(fp.TargetPlayer.ToString(), 0), "0.73 0.55", ".83 .79");
                        UI.CreateLabel(ref element, PanelFactions, UIColors["white"], GetDisplayName(fp.TargetPlayer), 16, "0.7 0.49", ".86 .55", TextAnchor.UpperCenter);
                        UI.LoadImage(ref element, PanelFactions, TryForImage("hourglass"), "0.73 .41", ".76 .49");
                        UI.CreateLabel(ref element, PanelFactions, UIColors["white"], GetMSG("PlayTimeMinutes", player, Playerdata.RustLifeTime.ToString(), Playerdata.RustTwoWeekTime.ToString()), 10, "0.77 .39", ".86 .49", TextAnchor.MiddleLeft);
                        UI.CreateLabel(ref element, PanelFactions, UIColors["white"], BasePlayer.activePlayerList.Contains(BasePlayer.FindByID(fp.TargetPlayer)) ? GetMSG("ONLINE") : GetMSG("OFFLINE"), 16, "0.7 .34", ".86 .39");
                        if (fdata.Players[fp.TargetPlayer].faction != 0)
                        {
                            var factiondet = fdata.Factions[fdata.Players[fp.TargetPlayer].faction];
                            UI.CreateTextOutline(ref element, PanelFactions, factiondet.UIColor, UIColors["white"], factiondet.Name, 16, ".76 .26", ".86 .36", TextAnchor.MiddleLeft);
                            if (!string.IsNullOrEmpty(factiondet.embleem))
                                UI.LoadImage(ref element, PanelFactions, TryForImage(factiondet.embleem), "0.7 0.26", ".76 .36");
                        }
                        if (isAuth(player) && AdminView.Contains(player.userID))
                        {
                            i = 8;
                            pos = RightPanelPos(i);
                            if (fdata.Players[fp.TargetPlayer].faction != 0)
                            {
                                UI.LoadImage(ref element, PanelFactions, TryForImage("DarkButton"), $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}");
                                UI.CreateLabel(ref element, PanelFactions, UIColors["white"], GetMSG("KickFromFaction"), 14, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}");
                                UI.CreateButton(ref element, PanelFactions, "0 0 0 0", "", 8, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"UI_FC_KickPlayer", TextAnchor.MiddleCenter);
                            }
                            else
                            {
                                UI.LoadImage(ref element, PanelFactions, TryForImage("LightButton"), $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}");
                                UI.CreateLabel(ref element, PanelFactions, UIColors["white"], GetMSG("AssignToFaction"), 14, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}");
                                UI.CreateButton(ref element, PanelFactions, "0 0 0 0", "", 8, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"UI_FC_AssignPlayer", TextAnchor.MiddleCenter);
                            }
                        }
                        else if (faction != 0 && (isOwner(player, faction) || isModerator(player, faction)))
                        {
                            if (fdata.Players[fp.TargetPlayer].faction == faction)
                            {
                                i = 7;
                                pos = RightPanelPos(i);
                                if (!fdata.Factions[faction].Moderators.Contains(fp.TargetPlayer))
                                {
                                    UI.LoadImage(ref element, PanelFactions, TryForImage("LightButton"), $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}");
                                    UI.CreateLabel(ref element, PanelFactions, UIColors["white"], GetMSG("AddModerator"), 14, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}");
                                    UI.CreateButton(ref element, PanelFactions, "0 0 0 0", "", 8, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"UI_FC_Moderator add", TextAnchor.MiddleCenter);
                                    i++;
                                }
                                else
                                {
                                    UI.LoadImage(ref element, PanelFactions, TryForImage("DarkButton"), $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}");
                                    UI.CreateLabel(ref element, PanelFactions, UIColors["white"], GetMSG("RemoveModerator"), 14, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}");
                                    UI.CreateButton(ref element, PanelFactions, "0 0 0 0", "", 8, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"UI_FC_Moderator remove", TextAnchor.MiddleCenter);
                                    i++;
                                }
                                i = 8;
                                pos = RightPanelPos(i);
                                UI.LoadImage(ref element, PanelFactions, TryForImage("DarkButton"), $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}");
                                UI.CreateLabel(ref element, PanelFactions, UIColors["white"], GetMSG("KickFromFaction"), 14, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}");
                                UI.CreateButton(ref element, PanelFactions, "0 0 0 0", "", 8, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"UI_FC_KickPlayer", TextAnchor.MiddleCenter);
                            }
                            else if (fdata.Players[fp.TargetPlayer].faction == 0 && !fdata.Players[fp.TargetPlayer].PendingInvites.Contains(faction))
                            {
                                i = 8;
                                pos = RightPanelPos(i);
                                UI.LoadImage(ref element, PanelFactions, TryForImage("LightButton"), $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}");
                                UI.CreateLabel(ref element, PanelFactions, UIColors["white"], GetMSG("InviteToFaction"), 14, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}");
                                UI.CreateButton(ref element, PanelFactions, "0 0 0 0", "", 8, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"UI_FC_SendInvite", TextAnchor.MiddleCenter);
                            }
                        }
                    }
                    break;

                case "SCOREBOARD":

                    break;
                case "Assign":
                    if (fdata.Factions.Count() == 0) return;
                    count = fdata.Factions.Count();
                    UI.CreateTextOutline(ref element, PanelFactions, UIColors["white"], UIColors["black"], GetMSG("FactionSelectionTitle", player), 18, "0.315 0.8", ".685 .85", TextAnchor.UpperCenter);
                    entriesallowed = 38;
                    remainingentries = count - (page * entriesallowed);
                    if (remainingentries > entriesallowed)
                    {
                        UI.LoadImage(ref element, PanelFactions, TryForImage("NEXT"), "0.54 0.03", "0.59 0.3");
                        UI.CreateButton(ref element, PanelFactions, "0 0 0 0", GetMSG("Next"), 18, "0.54 0.03", "0.59 0.13", $"UI_FC_TurnPage {page + 1}");
                    }
                    if (page > 0)
                    {
                        UI.LoadImage(ref element, PanelFactions, TryForImage("BACK"), "0.61 0.03", "0.66 0.3");
                        UI.CreateButton(ref element, PanelFactions, "0 0 0 0", GetMSG("Back"), 18, "0.61 0.03", "0.66 0.13", $"UI_FC_TurnPage {page - 1}");
                    }
                    shownentries = page * entriesallowed;
                    i = 0;
                    n = 0;
                    foreach (var entry in fdata.Factions)
                    {
                        i++;
                        if (i < shownentries + 1) continue;
                        else if (i <= shownentries + entriesallowed)
                        {
                            pos = MiddlePanelPos(n);
                            UI.CreateButton(ref element, PanelFactions, entry.Value.UIColor, entry.Value.Name, 12, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"UI_FC_AssignPlayer {entry.Key}"); n++;
                        }
                    }
                    break;
                case "OPTIONS":
                    i = 0;
                    UI.CreateTextOutline(ref element, PanelFactions, UIColors["white"], UIColors["black"], GetMSG("Options", player), 18, "0.315 0.8", ".685 .85", TextAnchor.UpperCenter);
                    var possiblepages = 1;
                    if (possiblepages > 1 && page < possiblepages)

                    {
                        UI.LoadImage(ref element, PanelFactions, TryForImage("NEXT"), "0.54 0.03", "0.59 0.3");
                        UI.CreateButton(ref element, PanelFactions, "0 0 0 0", GetMSG("Next"), 18, "0.54 0.03", "0.59 0.13", $"UI_FC_TurnPage {page + 1}");
                    }
                    if (page > 0)
                    {
                        UI.LoadImage(ref element, PanelFactions, TryForImage("BACK"), "0.61 0.03", "0.66 0.3");
                        UI.CreateButton(ref element, PanelFactions, "0 0 0 0", GetMSG("Back"), 18, "0.61 0.03", "0.66 0.13", $"UI_FC_TurnPage {page - 1}");
                    }
                    var button = string.Empty;
                    if (page == 0)
                    {
                        if (configData.AutoAuthorization) button = "GreenSquareButton";
                        else button = "RedSquareButton";
                        CreateOptionButton(ref element, PanelFactions, button, GetMSG("AutoAuthorizationTitle"), $"UI_ChangeOption UI_AutoAuthorization AutoAuthorizationInfo AutoAuthorizationTitle {configData.AutoAuthorization}", i); i++;

                        if (configData.AuthorizeLeadersOnly) button = "GreenSquareButton";
                        else button = "RedSquareButton";
                        CreateOptionButton(ref element, PanelFactions, button, GetMSG("AuthorizeLeadersOnlyTitle"), $"UI_ChangeOption UI_AuthorizeLeadersOnly AuthorizeLeadersOnlyInfo AuthorizeLeadersOnlyTitle {configData.AuthorizeLeadersOnly}", i); i++;

                        if (configData.SafeZones_Allow) button = "GreenSquareButton";
                        else button = "RedSquareButton";
                        CreateOptionButton(ref element, PanelFactions, button, GetMSG("SafeZones_AllowTitle"), $"UI_ChangeOption UI_SafeZones_Allow SafeZones_AllowInfo SafeZones_AllowTitle {configData.SafeZones_Allow}", i); i++;

                        if (configData.DeleteEmptyFactions) button = "GreenSquareButton";
                        else button = "RedSquareButton";
                        CreateOptionButton(ref element, PanelFactions, button, GetMSG("DeleteEmptyFactionsTitle"), $"UI_ChangeOption UI_DeleteEmptyFactions DeleteEmptyFactionsInfo DeleteEmptyFactionsTitle {configData.DeleteEmptyFactions}", i); i++;

                        if (configData.DisableMenu) button = "GreenSquareButton";
                        else button = "RedSquareButton";
                        CreateOptionButton(ref element, PanelFactions, button, GetMSG("DisableMenuTitle"), $"UI_ChangeOption UI_DisableMenu DisableMenuInfo DisableMenuTitle {configData.DisableMenu}", i); i++;

                        if (configData.Use_FactionAnnouncements) button = "GreenSquareButton";
                        else button = "RedSquareButton";
                        CreateOptionButton(ref element, PanelFactions, button, GetMSG("Use_FactionAnnouncementsTitle"), $"UI_ChangeOption UI_Use_FactionAnnouncements Use_FactionAnnouncementsInfo Use_FactionAnnouncementsTitle {configData.Use_FactionAnnouncements}", i); i++;

                        if (configData.Use_PrivateFactionChat) button = "GreenSquareButton";
                        else button = "RedSquareButton";
                        CreateOptionButton(ref element, PanelFactions, button, GetMSG("Use_PrivateFactionChatTitle"), $"UI_ChangeOption UI_Use_PrivateFactionChat Use_PrivateFactionChatInfo Use_PrivateFactionChatTitle {configData.Use_PrivateFactionChat}", i); i++;

                        if (configData.LockFactionKits_and_CustomSets) button = "GreenSquareButton";
                        else button = "RedSquareButton";
                        CreateOptionButton(ref element, PanelFactions, button, GetMSG("LockFactionKits_and_CustomSetsTitle"), $"UI_ChangeOption UI_LockFactionKits_and_CustomSets LockFactionKits_and_CustomSetsInfo LockFactionKits_and_CustomSetsTitle {configData.LockFactionKits_and_CustomSets}", i); i++;

                        if (configData.Use_FactionChatControl) button = "GreenSquareButton";
                        else button = "RedSquareButton";
                        CreateOptionButton(ref element, PanelFactions, button, GetMSG("ChatControlTitle"), $"UI_ChangeOption UI_Use_FactionChatControl ChatControlInfo ChatControlTitle {configData.Use_FactionChatControl}", i); i++;

                        if (configData.Use_FactionTags) button = "GreenSquareButton";
                        else button = "RedSquareButton";
                        CreateOptionButton(ref element, PanelFactions, button, GetMSG("FactionTagOnChatTitle"), $"UI_ChangeOption UI_Use_FactionTags FactionTagOnChatInfo FactionTagOnChatTitle {configData.Use_FactionTags}", i); i++;

                        if (configData.Use_PlayerListMenu) button = "GreenSquareButton";
                        else button = "RedSquareButton";
                        CreateOptionButton(ref element, PanelFactions, button, GetMSG("Use_PlayerListMenuTitle"), $"UI_ChangeOption UI_Use_PlayerListMenu Use_PlayerListMenuInfo Use_PlayerListMenuTitle {configData.Use_PlayerListMenu}", i); i++;

                        if (configData.Allow_FriendlyFire) button = "GreenSquareButton";
                        else button = "RedSquareButton";
                        CreateOptionButton(ref element, PanelFactions, button, GetMSG("FriendlyFireTitle"), $"UI_ChangeOption UI_FriendlyFire FriendlyFireInfo FriendlyFireTitle {configData.Allow_FriendlyFire}", i); i++;

                        if (configData.Use_AllowPlayersToCreateFactions) button = "GreenSquareButton";
                        else button = "RedSquareButton";
                        CreateOptionButton(ref element, PanelFactions, button, GetMSG("PlayersToCreateFactionsTitle"), $"UI_ChangeOption UI_Use_AllowPlayersToCreateFactionsTitle PlayersToCreateFactionsInfo PlayersToCreateFactionsTitle {configData.Use_AllowPlayersToCreateFactions}", i); i++;

                        if (configData.ShowFactionPlayersOnMap) button = "GreenSquareButton";
                        else button = "RedSquareButton";
                        CreateOptionButton(ref element, PanelFactions, button, GetMSG("ShowFactionPlayersOnMapTitle "), $"UI_ChangeOption UI_Use_ShowFactionPlayersOnMap ShowFactionPlayersOnMapInfo ShowFactionPlayersOnMapTitle  {configData.ShowFactionPlayersOnMap}", i); i++;
                    }
                    if (page == 2)
                    {
                    }

                    if (page == 3)
                    {
                    }
                    pos = LeftPanelPos(7);
                    UI.LoadImage(ref element, PanelFactions, TryForImage("LightButton"), $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}");
                    UI.CreateLabel(ref element, PanelFactions, UIColors["white"], GetMSG("CreateFaction"), 14, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}");
                    UI.CreateButton(ref element, PanelFactions, "0 0 0 0", "", 8, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"UI_NewFaction");
                    pos = LeftPanelPos(8);
                    UI.LoadImage(ref element, PanelFactions, TryForImage("LightButton"), $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}");
                    UI.CreateLabel(ref element, PanelFactions, UIColors["white"], GetMSG("ToggleAdminView", player), 14, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}");
                    UI.CreateButton(ref element, PanelFactions, "0 0 0 0", "", 14, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"UI_ToggleAdminView");
                    break;
            }
            CuiHelper.AddUi(player, element);
        }

        private string GetDisplayName(ulong UserID)
        {
            IPlayer player = this.covalence.Players.FindPlayer(UserID.ToString());
            if (player == null) return UserID.ToString();
            return player.Name;
        }

        private float[] LeftPanelPos(int number)
        {
            Vector2 position = new Vector2(0.15f, 0.71f);
            Vector2 dimensions = new Vector2(0.14f, 0.07f);
            float offsetY = 0;
            float offsetX = 0;
            offsetY = (-0.015f - dimensions.y) * number;
            Vector2 offset = new Vector2(offsetX, offsetY);
            Vector2 posMin = position + offset;
            Vector2 posMax = posMin + dimensions;
            return new float[] { posMin.x, posMin.y, posMax.x, posMax.y };
        }

        private float[] RightPanelPos(int number)
        {
            Vector2 position = new Vector2(0.71f, 0.71f);
            Vector2 dimensions = new Vector2(0.14f, 0.07f);
            float offsetY = 0;
            float offsetX = 0;
            offsetY = (-0.015f - dimensions.y) * number;
            Vector2 offset = new Vector2(offsetX, offsetY);
            Vector2 posMin = position + offset;
            Vector2 posMax = posMin + dimensions;
            return new float[] { posMin.x, posMin.y, posMax.x, posMax.y };
        }


        private float[] MiddlePanelPos(int number)
        {
            Vector2 position = new Vector2(0.325f, 0.68f);
            Vector2 dimensions = new Vector2(0.065f, 0.07f);
            float offsetY = 0;
            float offsetX = 0;
            if (number >= 0 && number < 5)
            {
                offsetX = (0.005f + dimensions.x) * number;
            }
            if (number > 4 && number < 10)
            {
                offsetX = (0.005f + dimensions.x) * (number - 5);
                offsetY = (-0.02f - dimensions.y) * 1;
            }
            if (number > 9 && number < 15)
            {
                offsetX = (0.005f + dimensions.x) * (number - 10);
                offsetY = (-0.02f - dimensions.y) * 2;
            }
            if (number > 14 && number < 20)
            {
                offsetX = (0.005f + dimensions.x) * (number - 15);
                offsetY = (-0.02f - dimensions.y) * 3;
            }
            if (number > 19 && number < 25)
            {
                offsetX = (0.005f + dimensions.x) * (number - 20);
                offsetY = (-0.02f - dimensions.y) * 4;
            }
            if (number > 24 && number < 30)
            {
                offsetX = (0.005f + dimensions.x) * (number - 25);
                offsetY = (-0.02f - dimensions.y) * 5;
            }
            if (number > 29 && number < 35)
            {
                offsetX = (0.005f + dimensions.x) * (number - 30);
                offsetY = (-0.02f - dimensions.y) * 6;
            }
            if (number > 34 && number < 40)
            {
                offsetX = (0.005f + dimensions.x) * (number - 35);
                offsetY = (-0.02f - dimensions.y) * 7;
            }
            Vector2 offset = new Vector2(offsetX, offsetY);
            Vector2 posMin = position + offset;
            Vector2 posMax = posMin + dimensions;
            return new float[] { posMin.x, posMin.y, posMax.x, posMax.y };
        }

        private float[] OptionButtonPos(int number)
        {
            Vector2 position = new Vector2(0.325f, 0.66f);
            Vector2 dimensions = new Vector2(0.11f, 0.11f);
            float offsetY = 0;
            float offsetX = 0;
            if (number >= 0 && number < 3)
            {
                offsetX = (0.005f + dimensions.x) * number;
            }
            if (number > 2 && number < 6)
            {
                offsetX = (0.005f + dimensions.x) * (number - 3);
                offsetY = (-0.02f - dimensions.y) * 1;
            }
            if (number > 5 && number < 9)
            {
                offsetX = (0.005f + dimensions.x) * (number - 6);
                offsetY = (-0.02f - dimensions.y) * 2;
            }
            if (number > 8 && number < 12)
            {
                offsetX = (0.005f + dimensions.x) * (number - 9);
                offsetY = (-0.02f - dimensions.y) * 3;
            }
            if (number > 11 && number < 15)
            {
                offsetX = (0.005f + dimensions.x) * (number - 12);
                offsetY = (-0.02f - dimensions.y) * 4;
            }
            Vector2 offset = new Vector2(offsetX, offsetY);
            Vector2 posMin = position + offset;
            Vector2 posMax = posMin + dimensions;
            return new float[] { posMin.x, posMin.y, posMax.x, posMax.y };
        }


        private object GetBasePlayer(ulong ID)
        {
            try
            {
                BasePlayer player = BasePlayer.FindByID(ID);
                if (BasePlayer.activePlayerList.Contains(player))
                    return player;
                else return null;
            }
            catch
            {
                return null;
            }
        }

        [ConsoleCommand("UI_FC_TogglePrivate")]
        void cmdUI_FC_TogglePrivate(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            FactionPlayer fp = GetFactionPlayer(player);
            if (fp == null) return;
            if (!isOwner(player, fp.Faction) && !isModerator(player, fp.Faction)) return;
            if (fdata.Factions[fp.Faction].Private)
            {
                fdata.Factions[fp.Faction].Private = false;
                OnScreen(player, "NoLongerPrivate");
            }
            else
            {
                fdata.Factions[fp.Faction].Private = true;
                OnScreen(player, "NowPrivate");
            }
            FactionPanel(player);
        }

        [ConsoleCommand("UI_FC_SendInvite")]
        private void cmdUI_FC_SendInvite(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null) return;
            if (!isOwner(player, GetFactionPlayer(player).Faction)) return;
            var faction = GetFactionPlayer(player).Faction;
            if (GetFactionPlayer(player).TargetPlayer == new ulong()) return;
            if (!fdata.Players[GetFactionPlayer(player).TargetPlayer].PendingInvites.Contains(faction))
                fdata.Players[GetFactionPlayer(player).TargetPlayer].PendingInvites.Add(faction);
            GetFactionPlayer(player).TargetPlayer = new ulong();
            FactionPanel(player);
        }

        [ConsoleCommand("UI_FC_Moderator")]
        private void cmdUI_FC_Moderator(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            if (!isAuth(player) && !isOwner(player, GetFactionPlayer(player).Faction)) return;
            var faction = fdata.Factions[GetFactionPlayer(player).Faction];
            if (GetFactionPlayer(player).TargetPlayer == new ulong()) return;
            string Action = arg.Args[0];
            ulong target = GetFactionPlayer(player).TargetPlayer;
            if (Action == "add")
            {
                if (faction.factionPlayers.Contains(target) && !faction.Moderators.Contains(target))
                    faction.Moderators.Add(target);
            }
            else if (faction.factionPlayers.Contains(target) && faction.Moderators.Contains(target))
                faction.Moderators.Remove(target);
            FactionPanel(player);
        }


        [ConsoleCommand("UI_FC_KickPlayer")]
        private void cmdUI_FC_KickPlayer(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            if (!isAuth(player) && !isOwner(player, GetFactionPlayer(player).Faction) && !isModerator(player, GetFactionPlayer(player).Faction)) return;
            if (GetFactionPlayer(player).TargetPlayer == new ulong()) return;
            ulong target = GetFactionPlayer(player).TargetPlayer;
            UnassignPlayerFromFaction(target, true);
            if (!isAuth(player) || !AdminView.Contains(player.userID))
                GetFactionPlayer(player).TargetPlayer = new ulong();
            FactionPanel(player);
        }

        [ConsoleCommand("UI_FC_AssignPlayer")]
        private void cmdUI_AssignPlayer(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            if (!isAuth(player)) return;
            if (GetFactionPlayer(player).TargetPlayer == new ulong()) return;
            ulong target = GetFactionPlayer(player).TargetPlayer;
            if (arg.Args == null || arg.Args.Count() == 0)
                GetFactionPlayer(player).Panel = "Assign";
            else
            {
                var faction = Convert.ToUInt16(arg.Args[0]);
                BasePlayer Target = BasePlayer.FindByID(target);
                if (Target == null)
                {
                    var PlayerData = fdata.Players[target];
                    PlayerData.faction = faction;
                    PlayerData.PendingInvites.Clear();
                    if (!fdata.Factions[faction].factionPlayers.Contains(target))
                        fdata.Factions[faction].factionPlayers.Add(target);
                    if (fdata.Factions[faction].factionPlayers.Count() <= 1)
                        fdata.Factions[faction].Leader = target;
                    ConsoleSystem.Run(ConsoleSystem.Option.Server, $"usergroup add {target} {fdata.Factions[faction].group}");
                    SaveData();
                    Broadcast($"<color={GetMSG("JoinedFaction", player, fdata.Factions[faction].ChatColor, GetDisplayName(target), fdata.Factions[faction].Name)}</color>");
                }
                else
                {
                    AssignPlayerToFaction(Target, faction);
                }
                GetFactionPlayer(player).SelectedFaction = new ushort();
                GetFactionPlayer(player).Panel = "PLIST";
            }
            FactionPanel(player);
        }

        #endregion

        [ConsoleCommand("UI_ChangeOption")]
        private void cmdChangeOption(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            var cmd = arg.Args[0];
            var verbiage = arg.Args[1];
            var optionName = arg.Args[2];
            var status = arg.Args[3];
            ChangeOption(player, cmd, verbiage, optionName, status);
        }

        void ChangeOption(BasePlayer player, string cmd, string verbiage, string optionName, string status)
        {
            CuiHelper.DestroyUi(player, PanelOnScreen);
            string state = "";
            if (status.ToUpper() == "FALSE") state = lang.GetMessage($"FALSE", this);
            if (status.ToUpper() == "TRUE") state = lang.GetMessage($"TRUE", this);
            string title = string.Format(lang.GetMessage($"OptionChangeTitle", this), lang.GetMessage($"{optionName}", this), state);
            string change = "";
            if (status.ToUpper() == "FALSE") change = lang.GetMessage($"TRUE", this);
            else change = lang.GetMessage($"FALSE", this);
            var element = UI.CreateOverlayContainer(PanelOnScreen, UIColors["dark"], "0.3 0.3", "0.7 0.7", true);
            UI.CreatePanel(ref element, PanelOnScreen, UIColors["light"], "0.01 0.02", "0.99 0.98", true);
            UI.CreateLabel(ref element, PanelOnScreen, UIColors["header"], title, 18, "0.03 0.85", "0.97 .95", TextAnchor.UpperCenter);
            UI.CreateLabel(ref element, PanelOnScreen, UIColors["dark"], lang.GetMessage($"{verbiage}", this), 18, "0.03 0.27", "0.97 0.83", TextAnchor.UpperLeft);
            UI.CreateLabel(ref element, PanelOnScreen, UIColors["dark"], string.Format(lang.GetMessage($"OptionChangeMSG", this), change), 18, "0.2 0.18", "0.8 0.26", TextAnchor.MiddleCenter);
            UI.CreateButton(ref element, PanelOnScreen, UIColors["buttonbg"], "Yes", 18, "0.2 0.05", "0.4 0.15", $"{cmd}");
            UI.CreateButton(ref element, PanelOnScreen, UIColors["buttonred"], "No", 16, "0.6 0.05", "0.8 0.15", $"UI_DestroyOnScreenPanel");
            CuiHelper.AddUi(player, element);
        }

        private void CreateOptionButton(ref CuiElementContainer container, string panelName, string button, string name, string cmd, int num)
        {
            var pos = OptionButtonPos(num);
            UI.LoadImage(ref container, panelName, TryForImage(button), $"{ pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}");
            UI.CreateLabel(ref container, panelName, UIColors["black"], name, 12, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}");
            UI.CreateButton(ref container, panelName, "0 0 0 0", "", 12, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", cmd);
        }


        [ConsoleCommand("UI_DestroyOnScreenPanel")]
        private void cmdUI_DestroyOnScreenPanel(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null) return;
            CuiHelper.DestroyUi(player, PanelOnScreen);
        }

        [ConsoleCommand("UI_AutoAuthorization")]
        private void cmdUI_AutoAuthorization(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            if (configData.AutoAuthorization == true) configData.AutoAuthorization = false;
            else configData.AutoAuthorization = true;
            FactionPanel(player);
            SaveConfig(configData);
        }

        [ConsoleCommand("UI_AuthorizeLeadersOnly")]
        private void cmdUI_AuthorizeLeadersOnly(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            if (configData.AuthorizeLeadersOnly == true) configData.AuthorizeLeadersOnly = false;
            else configData.AuthorizeLeadersOnly = true;
            FactionPanel(player);
            SaveConfig(configData);
        }
        [ConsoleCommand("UI_DeleteEmptyFactions")]
        private void cmdUI_DeleteEmptyFactions(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            if (configData.DeleteEmptyFactions == true) configData.DeleteEmptyFactions = false;
            else configData.DeleteEmptyFactions = true;
            FactionPanel(player);
            SaveConfig(configData);
        }
        [ConsoleCommand("UI_SafeZones_Allow")]
        private void cmdUI_SafeZones_Allow(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            if (configData.SafeZones_Allow == true) configData.SafeZones_Allow = false;
            else configData.SafeZones_Allow = true;
            FactionPanel(player);
            SaveConfig(configData);
        }
        [ConsoleCommand("UI_DisableMenu")]
        private void cmdUI_DisableMenu(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            if (configData.DisableMenu == true) configData.DisableMenu = false;
            else configData.DisableMenu = true;
            FactionPanel(player);
            SaveConfig(configData);
        }
        [ConsoleCommand("UI_Use_FactionAnnouncements")]
        private void cmdUI_Use_FactionAnnouncements(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            if (configData.Use_FactionAnnouncements == true) configData.Use_FactionAnnouncements = false;
            else configData.Use_FactionAnnouncements = true;
            FactionPanel(player);
            SaveConfig(configData);
        }
        [ConsoleCommand("UI_Use_PrivateFactionChat")]
        private void cmdUI_Use_PrivateFactionChat(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            if (configData.Use_PrivateFactionChat == true) configData.Use_PrivateFactionChat = false;
            else configData.Use_PrivateFactionChat = true;
            FactionPanel(player);
            SaveConfig(configData);
        }
        [ConsoleCommand("UI_Use_ShowFactionPlayersOnMap")]
        private void cmdUI_Use_ShowFactionPlayersOnMap(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            if (configData.ShowFactionPlayersOnMap == true) configData.ShowFactionPlayersOnMap = false;
            else configData.ShowFactionPlayersOnMap = true;
            FactionPanel(player);
            SaveConfig(configData);
        }
        [ConsoleCommand("UI_LockFactionKits_and_CustomSets")]
        private void cmdUI_LockFactionKits_and_CustomSets(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            if (configData.LockFactionKits_and_CustomSets == true) configData.LockFactionKits_and_CustomSets = false;
            else configData.LockFactionKits_and_CustomSets = true;
            FactionPanel(player);
            SaveConfig(configData);
        }

        [ConsoleCommand("UI_Use_FactionChatControl")]
        private void cmdFactionChatControl(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            if (configData.Use_FactionChatControl == true) configData.Use_FactionChatControl = false;
            else configData.Use_FactionChatControl = true;
            FactionPanel(player);
            SaveConfig(configData);
        }

        [ConsoleCommand("UI_Use_FactionTags")]
        private void cmdUI_Use_FactionTags(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            if (configData.Use_FactionTags == true) configData.Use_FactionTags = false;
            else configData.Use_FactionTags = true;
            FactionPanel(player);
            SaveConfig(configData);
        }

        [ConsoleCommand("UI_Use_AllowPlayersToCreateFactionsTitle")]
        private void cmdUI_Use_AllowPlayersToCreateFactionsTitle(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            if (configData.Use_AllowPlayersToCreateFactions == true) configData.Use_AllowPlayersToCreateFactions = false;
            else configData.Use_AllowPlayersToCreateFactions = true;
            FactionPanel(player);
            SaveConfig(configData);
        }


        [ConsoleCommand("UI_Use_PlayerListMenu")]
        private void cmdShowOnlinePlayers(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            if (configData.Use_PlayerListMenu == true) configData.Use_PlayerListMenu = false;
            else configData.Use_PlayerListMenu = true;
            foreach (BasePlayer p in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, PanelPlayer);
                PlayerPanel(player);
            }
            FactionPanel(player);
            SaveConfig(configData);
        }

        [ConsoleCommand("UI_FriendlyFire")]
        private void cmdFriendlyFire(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            if (configData.Allow_FriendlyFire == true) configData.Allow_FriendlyFire = false;
            else configData.Allow_FriendlyFire = true;
            FactionPanel(player);
            SaveConfig(configData);
        }

        #region UI Commands

        private int GetRandomNumber()
        {
            var random = new System.Random();
            int number = random.Next(int.MinValue, int.MaxValue);
            return number;
        }


        [ConsoleCommand("UI_NewFaction")]
        private void cmdNewFaction(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            if (!isAuth(player) && GetFactionPlayer(player).Faction != 0)
            {
                GetSendMSG(player, "InAFaction");
                return;
            }
            if (FactionDetails.ContainsKey(player.userID))
                FactionDetails.Remove(player.userID);
            ushort index = (ushort)GetRandomNumber();
            while (fdata.Factions.ContainsKey(index))
            {
                index++;
            }
            FactionDetails.Add(player.userID, new FactionDesigner { ID = index, creating = true, faction = new Faction { }, stepNum = 0 });
            DestroyUI(player);
            FactionCreation(player);
        }

        [ConsoleCommand("UI_Description")]
        private void cmdUI_Description(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            var type = arg.Args[0];
            if (type == "save")
            {
                FactionCreation(player, 99);
            }
            else if (type == "continue")
            {
                FactionCreation(player, 7);
            }
        }

        [ConsoleCommand("UI_SelectColor")]
        private void cmdSelectColor(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            var Color = arg.Args[0];
            FactionDesigner Creator;
            if (FactionDetails.ContainsKey(player.userID))
                Creator = FactionDetails[player.userID];
            else Creator = FactionEditor[player.userID];
            Creator.faction.ChatColor = Color;
            Creator.faction.UIColor = HexTOUIColor(Color);
            Creator.faction.group = Creator.faction.Name;
            DestroyUI(player);
            if (FactionEditor.ContainsKey(player.userID))
            {
                FactionCreation(player, 20);
                return;
            }
            Creator.stepNum = 3;
            FactionCreation(player, 4);
        }

        [ConsoleCommand("UI_SelectKit")]
        private void cmdUI_SelectKit(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            var kit = string.Join(" ", arg.Args);
            FactionDesigner Creator;
            if (FactionDetails.ContainsKey(player.userID))
                Creator = FactionDetails[player.userID];
            else Creator = FactionEditor[player.userID];
            Creator.faction.KitorSet = kit;
            DestroyUI(player);
            if (FactionEditor.ContainsKey(player.userID))
            {
                FactionCreation(player, 20);
                return;
            }
            Creator.stepNum = 3;
            FactionCreation(player, 5);
        }


        [ConsoleCommand("UI_ViewKit")]
        private void cmdUI_ViewKit(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            var kit = string.Join(" ", arg.Args);
            CuiHelper.DestroyUi(player, PanelOnScreen);
            var i = 0;
            var element = UI.CreateElementContainer(PanelOnScreen, "0 0 0 0", "0.71 0.3", "0.86 0.9");
            UI.CreateLabel(ref element, PanelOnScreen, "1 1 1 1", GetMSG("KitContents", player, kit), 16, "0 0.87", "1 0.97", TextAnchor.MiddleCenter);
            List<string> setcontents = new List<string>();
            setcontents = GetSetContents(kit);
            if (setcontents == null) return;
            foreach (var item in setcontents)
            {
                var name = item.Substring(0, item.IndexOf('_'));
                ulong skin;
                if (!ulong.TryParse(item.Substring(item.IndexOf('_') + 1, item.Length - (item.IndexOf('_') + 5)), out skin)) skin = 0;
                var pos = CalcEquipPos(i);
                UI.LoadImage(ref element, PanelOnScreen, TryForImage(name, skin), $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}");
                i++;
            }
            UI.CreateButton(ref element, PanelOnScreen, UIColors["buttonred"], GetMSG("Close", player), 14, "0.67 0.02", "0.97 0.08", $"UI_FC_DestroyOnScreenPanel");
            CuiHelper.AddUi(player, element);
        }


        [ConsoleCommand("UI_FC_DestroyOnScreenPanel")]
        private void cmdUI_FC_DestroyOnScreenPanel(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null) return;
            CuiHelper.DestroyUi(player, PanelOnScreen);
        }

        [ConsoleCommand("UI_SelectEmbleem")]
        private void cmdUI_SelectEmbleem(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            int index;
            if (!int.TryParse(arg.Args[0], out index)) return;
            FactionDesigner Creator;
            if (FactionDetails.ContainsKey(player.userID))
                Creator = FactionDetails[player.userID];
            else Creator = FactionEditor[player.userID];
            Creator.faction.embleem = $"Embleem{index}";
            DestroyUI(player);
            if (FactionEditor.ContainsKey(player.userID))
            {
                FactionCreation(player, 20);
                return;
            }
            Creator.stepNum = 1;
            FactionCreation(player, 1);
        }

        [ConsoleCommand("UI_SaveFaction")]
        private void cmdUI_SaveFaction(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            if (FactionDetails.ContainsKey(player.userID))
                SaveFaction(player);
        }

        [ConsoleCommand("UI_DeleteFaction")]
        private void cmdUI_DeleteFaction(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            if (!player.IsAdmin) return;
            ushort ID;
            if (!ushort.TryParse(arg.Args[0], out ID))
            {
                if (arg.Args[0] == "yes")
                {
                    ID = Convert.ToUInt16(arg.Args[1]);
                    if (!fdata.Factions.ContainsKey(ID)) return;
                    fdata.Factions.Remove(ID);
                }
                ManageFactionMenu(player);
                return;
            }
            else
            {
                ConfirmFactionDeletion(player, ID);
                return;
            }
        }


        [ConsoleCommand("UI_ExitFactionCreation")]
        private void cmdUI_ExitFactionCreation(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            ExitFactionCreation(player);
        }

        private void ExitFactionCreation(BasePlayer player)
        {
            if (FactionDetails.ContainsKey(player.userID))
                FactionDetails.Remove(player.userID);
            else FactionEditor.Remove(player.userID);
            GetSendMSG(player, "QuitFactionCreation");
            DestroyUI(player);
        }
        private void SaveFaction(BasePlayer player)
        {
            FactionDesigner Creator;
            if (FactionDetails.ContainsKey(player.userID))
            {
                Creator = FactionDetails[player.userID];
                var faction = Creator.ID;
                fdata.Factions.Add(Creator.ID, Creator.faction);
                FactionDetails.Remove(player.userID);
                GetSendMSG(player, "NewFactionCreated", Creator.faction.Name);
                if (GetFactionPlayer(player).Faction == 0 && !AdminView.Contains(player.userID))
                {
                    AssignPlayerToFaction(player, faction);
                }
                ConsoleSystem.Run(ConsoleSystem.Option.Server, $"group add {fdata.Factions[faction].group}");
            }
            else if (FactionEditor.ContainsKey(player.userID))
            {
                Creator = FactionEditor[player.userID];
                fdata.Factions.Remove(Creator.ID);
                fdata.Factions.Add(Creator.ID, Creator.faction);
                FactionEditor.Remove(player.userID);
                GetSendMSG(player, "FactionEdited", Creator.faction.Name);
            }
            DestroyUI(player);
            SaveData();
        }

        [ConsoleCommand("UI_FC_TurnPage")]
        private void cmdUI_ChangePage(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            if (!GetFactionPlayer(player).open) return;
            int page = Convert.ToInt32(arg.Args[0]);
            GetFactionPlayer(player).page = page;
            FactionPanel(player);
        }

        [ConsoleCommand("UI_FC_ChangePanel")]
        private void cmdUI_FC_ChangePanel(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null) return;
            FactionPlayer fp = GetFactionPlayer(player);
            if (fp == null) { InitializeFactionPlayer(player); return; }
            var panel = arg.Args[0];
            if (panel != fp.Panel)
            {
                if (panel == "MAP" && !configData.Use_Map)
                {
                    GetSendMSG(player, "MapDisabled");
                    return;
                }
                    fp.Panel = panel;
                fp.page = 0;
                fp.TargetPlayer = new ulong();
            }
            if (arg.Args != null && arg.Args.Length > 1)
            {
                ulong id;
                if (ulong.TryParse(arg.Args[1], out id)) fp.TargetPlayer = id;
                ushort Fid;
                if (ushort.TryParse(arg.Args[1], out Fid)) fp.SelectedFaction = Fid;
            }
            if (Professions)
                player.SendConsoleCommand("UI_ToggleProfessionsMenu close");
            if (CustomSets)
                player.SendConsoleCommand("ToggleCSUI close");
            FactionPanel(player);
        }

        [ConsoleCommand("UI_FactionInfo")]
        private void cmdUI_FactionInfo(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            ushort faction = Convert.ToUInt16(arg.Args[0]);
            GetFactionPlayer(player).SelectedFaction = faction;
            FactionPanel(player);
        }



        [ConsoleCommand("UI_ToggleOnlineOnly")]
        private void cmdUI_ToggleOnlineOnly(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            if (GetFactionPlayer(player).OnlineOnly)
                GetFactionPlayer(player).OnlineOnly = false;
            else
                GetFactionPlayer(player).OnlineOnly = true;
            GetFactionPlayer(player).TargetPlayer = new ulong();
            GetFactionPlayer(player).page = 0;
            FactionPanel(player);
        }

        [ConsoleCommand("UI_ToggleAdminView")]
        private void cmdUI_ToggleAdminView(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            if (!isAuth(player)) return;
            if (AdminView.Contains(player.userID))
            {
                AdminView.Remove(player.userID);
                OnScreen(player, "ExitAdminView");
            }
            else
            {
                AdminView.Add(player.userID);
                OnScreen(player, "EnterAdminView");
            }
        }

        [ConsoleCommand("UI_FactionSelection")]
        private void cmdFactionSelection(ConsoleSystem.Arg arg)
        {

            var player = arg.Connection.player as BasePlayer;
            if (player == null) return;
            ushort faction = GetFactionPlayer(player).SelectedFaction;
            var selectioncount = fdata.Factions[faction].factionPlayers.Count;
            if (configData.AllowedFactionDifference != 0)
            {
                var max = GetMax();
                var min = GetMin();
                int diff = max - min;
                if (max != 0 && selectioncount == max && diff >= configData.AllowedFactionDifference)
                {
                    GetSendMSG(player, "FactionToFull", fdata.Factions[faction].Name);
                    return;
                }
            }
            if (configData.FactionPlayerLimit != 0 && configData.FactionPlayerLimit <= selectioncount)
            {
                GetSendMSG(player, "FactionAtLimit", fdata.Factions[faction].Name);
                return;
            }
            DestroyUI(player);
            if (JoinCooldown.Contains(player.userID))
            { GetSendMSG(player, "FactionJoinCooldown"); return; }
            AssignPlayerToFaction(player, faction);
        }

        [ConsoleCommand("UI_AssignPlayerFaction")]
        private void cmdUI_AssignPlayerFaction(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            ushort faction = Convert.ToUInt16(arg.Args[0]);
            AssignPlayerToFaction(player, faction);
            CuiHelper.DestroyUi(player, PanelFactions);
        }

        [ConsoleCommand("UI_LeaveFaction")]
        private void cmdUI_LeaveFaction(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            var faction = GetFactionPlayer(player).Faction;
            var factionname = fdata.Factions[faction].Name;
            UnassignPlayerFromFaction(player.userID);
            SetJoinCooldown(player);
            PlayerPanel(player);
            FactionPanel(player);
        }

        private int GetMax()
        {
            int max = 0;
            foreach (var entry in fdata.Factions)
                if (entry.Value.factionPlayers.Count > max) max = entry.Value.factionPlayers.Count;
            return max;
        }

        private int GetMin()
        {
            int min = 999999;
            foreach (var entry in fdata.Factions)
                if (entry.Value.factionPlayers.Count < min) min = entry.Value.factionPlayers.Count;
            return min;
        }

        private void SetJoinCooldown(BasePlayer player)
        {
            if (JoinCooldown.Contains(player.userID))
                JoinCooldown.Remove(player.userID);
            else
            {
                JoinCooldown.Add(player.userID);
                timers.Add(player.userID.ToString(), timer.Once(600, () => SetJoinCooldown(player)));
            }
        }


        private void AssignPlayerToFaction(BasePlayer player, ushort faction)
        {
            var p = GetFactionPlayer(player);
            var factionname = fdata.Factions[faction].Name;
            if (p != null)
            {
                if (p.Faction != 0)
                {
                    if (p.Faction == faction)
                        return;
                    else
                    {
                        fdata.Factions[p.Faction].factionPlayers.Remove(player.userID);
                        ConsoleSystem.Run(ConsoleSystem.Option.Server, $"usergroup remove {player.userID} {fdata.Factions[p.Faction].group}");
                    }
                }
                p.Faction = faction;
                p.SelectedFaction = new ushort();
                fdata.Players[player.userID].PendingInvites.Clear();
                fdata.Factions[faction].factionPlayers.Add(player.userID);
                if (fdata.Factions[faction].factionPlayers.Count() <= 1)
                    fdata.Factions[faction].Leader = player.userID;
                foreach (var fp in FactionPlayers.Where(kvp => kvp.Faction == faction && kvp.player.userID != player.userID))
                    UpdateFactionListLM(fp.player.userID, factionname, fdata.Factions[faction].factionPlayers);
                AddFactionListLM(player.userID, factionname, fdata.Factions[faction].factionPlayers);
                ConsoleSystem.Run(ConsoleSystem.Option.Server, $"usergroup add {player.userID} {fdata.Factions[faction].group}");
                SaveData();
                AuthorizePlayerOnTurrets(player);
                GiveFactionGear(p.player);
                Broadcast($"<color={GetMSG("JoinedFaction", player, fdata.Factions[faction].ChatColor, GetDisplayName(player.userID), factionname)}</color>");
            }
            else InitializeFactionPlayer(player);
        }

        private void UnassignPlayerFromFaction(ulong playerID, bool admin = false)
        {
            ushort oldFaction = 0;
            try
            {
                BasePlayer player = BasePlayer.FindByID(playerID);
                if (player != null)
                {
                    if (GetFactionPlayer(player) != null && GetFactionPlayer(player).Faction != 0)
                    {
                        if (LockedUniform.ContainsKey(player.userID))
                        {
                            foreach (var entry in LockedUniform[player.userID])
                            {
                                entry.Key.RemoveFromContainer();
                                entry.Key.Remove(0f);
                            }
                            LockedUniform.Remove(player.userID);
                        }
                        oldFaction = GetFactionPlayer(player).Faction;
                        GetFactionPlayer(player).Faction = 0;
                        fdata.Factions[oldFaction].factionPlayers.Remove(playerID);
                        foreach (var fp in FactionPlayers.Where(kvp => kvp.Faction == oldFaction && kvp.player.userID != player.userID))
                            UpdateFactionListLM(fp.player.userID, fdata.Factions[oldFaction].Name, fdata.Factions[oldFaction].factionPlayers);
                        RemoveFactionListLM(player.userID, fdata.Factions[oldFaction].Name);
                        fdata.Factions[oldFaction].BanList.Add(player.userID);
                        UNAuthorizePlayerOnTurrets(player, oldFaction);
                    }
                }
                if (admin)
                {
                    OnScreen(player, "RemovedFromFaction", fdata.Factions[oldFaction].Name);
                }
            }
            catch
            {
                oldFaction = fdata.Players[playerID].faction;
                fdata.Players[playerID].faction = 0;
                if (fdata.Factions[oldFaction].factionPlayers.Contains(playerID))
                    fdata.Factions[oldFaction].factionPlayers.Remove(playerID);
            }
            BroadcastFaction(null, $"<color={GetMSG("LeftTheFaction", null, fdata.Factions[oldFaction].ChatColor, GetDisplayName(playerID), fdata.Factions[oldFaction].Name)}</color>", oldFaction);
            if (configData.DeleteEmptyFactions)
                if (fdata.Factions[oldFaction].factionPlayers.Count() == 0)
                {
                    ZoneManager.Call("EraseZone", oldFaction.ToString());
                    UnShadeZone(null, oldFaction.ToString());
                    RemoveFaction(null, oldFaction);
                }
            SaveData();
        }


        #endregion

        #region Chat Commands
        [ChatCommand("faction")]
        private void cmdFaction(BasePlayer player, string command, string[] args)
        {
            if (player == null) return;
            if ((configData.DisableMenu || !permission.UserHasPermission(player.UserIDString, this.Title+".allow")) && !isAuth(player)) return;
            if (!initialized)
            {
                GetSendMSG(player, "Factions is still loading images! Try again shortly.");
                return;
            }
            FactionPlayer fp = GetFactionPlayer(player);
            if (fp == null) { InitializeFactionPlayer(player); return; }
            ToggleFCMenu(player);
        }
        #endregion

        #region UI Calculations

        private float[] CalcButtonPos(int number)
        {
            Vector2 position = new Vector2(0.02f, 0.8f);
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
                offsetY = (-0.025f - dimensions.y) * 1;
            }
            if (number > 11 && number < 18)
            {
                offsetX = (0.01f + dimensions.x) * (number - 12);
                offsetY = (-0.025f - dimensions.y) * 2;
            }
            if (number > 17 && number < 24)
            {
                offsetX = (0.01f + dimensions.x) * (number - 18);
                offsetY = (-0.025f - dimensions.y) * 3;
            }
            if (number > 23 && number < 30)
            {
                offsetX = (0.01f + dimensions.x) * (number - 24);
                offsetY = (-0.025f - dimensions.y) * 4;
            }
            Vector2 offset = new Vector2(offsetX, offsetY);
            Vector2 posMin = position + offset;
            Vector2 posMax = posMin + dimensions;
            return new float[] { posMin.x, posMin.y, posMax.x, posMax.y };
        }

        private float[] CalcEquipPos(int number)
        {
            Vector2 position = new Vector2(0f, 0.73f);
            Vector2 dimensions = new Vector2(0.3f, 0.15f);
            float offsetY = 0;
            float offsetX = 0;
            if (number >= 0 && number < 3)
            {
                offsetX = (0.05f + dimensions.x) * number;
            }
            if (number > 2 && number < 6)
            {
                offsetX = (0.05f + dimensions.x) * (number - 3);
                offsetY = (-0.001f - dimensions.y) * 1;
            }
            if (number > 5 && number < 9)
            {
                offsetX = (0.05f + dimensions.x) * (number - 6);
                offsetY = (-0.001f - dimensions.y) * 2;
            }
            if (number > 8 && number < 12)
            {
                offsetX = (0.05f + dimensions.x) * (number - 9);
                offsetY = (-0.001f - dimensions.y) * 3;
            }
            if (number > 11 && number < 15)
            {
                offsetX = (0.05f + dimensions.x) * (number - 12);
                offsetY = (-0.001f - dimensions.y) * 4;
            }
            if (number > 14 && number < 18)
            {
                offsetX = (0.05f + dimensions.x) * (number - 15);
                offsetY = (-0.001f - dimensions.y) * 5;
            }
            Vector2 offset = new Vector2(offsetX, offsetY);
            Vector2 posMin = position + offset;
            Vector2 posMax = posMin + dimensions;
            return new float[] { posMin.x, posMin.y, posMax.x, posMax.y };
        }

        private float[] CalcInvItemPos(int number)
        {
            Vector2 position = new Vector2(0.02f, 0.8f);
            Vector2 dimensions = new Vector2(0.16f, 0.16f);
            float offsetY = 0;
            float offsetX = 0;
            if (number >= 0 && number < 6)
            {
                offsetX = (dimensions.x + .001f) * number;
            }
            if (number > 5 && number < 12)
            {
                offsetX = (dimensions.x + .001f) * (number - 6);
                offsetY = (-0.005f - dimensions.y) * 1;
            }
            if (number > 11 && number < 18)
            {
                offsetX = (dimensions.x + .001f) * (number - 12);
                offsetY = (-0.005f - dimensions.y) * 2;
            }
            if (number > 17 && number < 24)
            {
                offsetX = (dimensions.x + .001f) * (number - 18);
                offsetY = (-0.005f - dimensions.y) * 3;
            }
            Vector2 offset = new Vector2(offsetX, offsetY);
            Vector2 posMin = position + offset;
            Vector2 posMax = posMin + dimensions;
            return new float[] { posMin.x, posMin.y, posMax.x, posMax.y };
        }

        //private void CreateFactionDetails(ref CuiElementContainer container, string panelName, string text, int number)
        //{
        //    Vector2 dimensions = new Vector2(0.8f, 0.1f);
        //    Vector2 origin = new Vector2(0.1f, 0.7f);
        //    Vector2 offset = new Vector2(0, (0.01f + dimensions.y) * number);

        //    Vector2 posMin = origin - offset;
        //    Vector2 posMax = posMin + dimensions;
        //    UI.CreateLabel(ref container, panelName, UIColors["buttonbg"], text, 18, $"{posMin.x} {posMin.y}", $"{posMax.x} {posMax.y}");
        //}

        private float[] PlayerEntryPos(int number)
        {
            Vector2 position = new Vector2(0.03f, 0.81f);
            Vector2 dimensions = new Vector2(0.2f, 0.15f);
            float offsetY = 0;
            float offsetX = 0;
            if (number >= 0 && number < 6)
            {
                offsetY = (-0.005f - dimensions.y) * number;
            }
            if (number > 5 && number < 12)
            {
                offsetX = (0.005f + dimensions.x) * 1;
                offsetY = (-0.005f - dimensions.y) * (number - 6);
            }
            if (number > 11 && number < 18)
            {
                offsetX = (0.005f + dimensions.x) * 2;
                offsetY = (-0.005f - dimensions.y) * (number - 12);
            }
            if (number > 17 && number < 24)
            {
                offsetX = (0.005f + dimensions.x) * 3;
                offsetY = (-0.005f - dimensions.y) * (number - 18);
            }
            Vector2 offset = new Vector2(offsetX, offsetY);
            Vector2 posMin = position + offset;
            Vector2 posMax = posMin + dimensions;
            return new float[] { posMin.x, posMin.y, posMax.x, posMax.y };
        }

        #endregion

        #region External Functions

        [HookMethod("BroadcastOnScreen")]
        void BroadcastOnScreen(string message, ushort Faction, ushort Faction2 = 0)
        {
            BroadcastOnScreenFaction(message, Faction, Faction2);
        }

        [HookMethod("AssignToFaction")]
        bool AssignToFaction(ulong ID, ushort Faction)
        {
            if (!fdata.Players.ContainsKey(ID)) return false;
            BasePlayer Target = BasePlayer.FindByID(ID);
            if (Target == null)
            {
                var PlayerData = fdata.Players[ID];
                PlayerData.faction = Faction;
                PlayerData.PendingInvites.Clear();
                fdata.Factions[Faction].factionPlayers.Add(ID);
                if (fdata.Factions[Faction].factionPlayers.Count() <= 1)
                    fdata.Factions[Faction].Leader = ID;
                ConsoleSystem.Run(ConsoleSystem.Option.Server, $"usergroup add {ID} {fdata.Factions[Faction].group}");
                SaveData();
                Broadcast($"<color={GetMSG("JoinedFaction", null, fdata.Factions[Faction].ChatColor, GetDisplayName(ID), fdata.Factions[Faction].Name)}</color>");
            }
            else
            {
                if (GetFactionPlayer(Target) == null) return false;
                AssignPlayerToFaction(Target, Faction);
            }
            return true;
        }

        [HookMethod("ClearFactionPlayers")]
        void ClearFactionPlayers(ushort ID)
        {
            if (!fdata.Factions.ContainsKey(ID)) return;
            foreach (var player in fdata.Factions[ID].factionPlayers)
            {
                try
                {
                    var fp = GetFactionPlayer(BasePlayer.FindByID(player));
                    if (fp != null)
                        fp.Faction = 0;
                }
                catch
                {
                    if (fdata.Players.ContainsKey(player))
                        fdata.Players[player].faction = 0;
                }
            }
            fdata.Factions[ID].factionPlayers.Clear();
            SaveData();
        }


        [HookMethod("GivePlayerFactionGear")]
        bool GivePlayerFactionGear(BasePlayer player)
        {
            GiveFactionGear(player);
            return true;
        }

        [HookMethod("IsFaction")]
        bool IsFaction(ushort ID)
        {
            if (ID != 0 && fdata.Factions.ContainsKey(ID)) return true;
            return false;
        }

        [HookMethod("GetPlayerFaction")]
        object GetPlayerFaction(ulong ID)
        {
            if (fdata.Players.ContainsKey(ID) && fdata.Players[ID].faction != 0) return fdata.Players[ID].faction;
            return false;
        }

        [HookMethod("GetFactionInfo")]
        object GetFactionInfo(ushort ID, string request)
        {
            if (ID == 0 || !fdata.Factions.ContainsKey(ID)) return false;
            switch (request.ToLower())
            {
                case "name":
                    return fdata.Factions[ID].Name;
                case "tag":
                    return fdata.Factions[ID].tag;
                case "group":
                    return fdata.Factions[ID].group;
                case "embleem":
                    return fdata.Factions[ID].embleem;
                case "description":
                    return fdata.Factions[ID].description;
                case "chatcolor":
                    return fdata.Factions[ID].ChatColor;
                case "uicolor":
                    return fdata.Factions[ID].UIColor;
                case "members":
                    return fdata.Factions[ID].factionPlayers;
                case "owner":
                    return fdata.Factions[ID].Leader;
                case "moderators":
                    return fdata.Factions[ID].Moderators;
                case "playercount":
                    return fdata.Factions[ID].factionPlayers.Count();
                default:
                    return false;
            }
        }

        [HookMethod("GetFactionList")]
        List<ushort> GetFactionList()
        {
            if (fdata.Factions == null || fdata.Factions.Count < 1)
                return null;
            List<ushort> list = new List<ushort>();
            foreach (var entry in fdata.Factions)
                list.Add(entry.Key);
            return list;
        }

        [HookMethod("CheckSameFaction")]
        bool CheckSameFaction(ulong player1ID, ulong player2ID)
        {
            if (!fdata.Players.ContainsKey(player1ID) || !fdata.Players.ContainsKey(player2ID)) return false;
            var player1faction = fdata.Players[player1ID].faction;
            var player2faction = fdata.Players[player2ID].faction;
            if (player1faction == player2faction && player1faction != 0) return true;
            else return false;

        }

        private void AddFactionListLM(ulong playerID, string FactionName, List<ulong> IDs)
        {
            if (!LustyMap) return;
            List<string> stringIDs = new List<string>();
            foreach (var entry in IDs)
                stringIDs.Add(entry.ToString());
            LustyMap?.Call("AddFriendList", playerID.ToString(), FactionName, stringIDs);
        }

        private void RemoveFactionListLM(ulong playerID, string FactionName)
        {
            if (!LustyMap) return;
            LustyMap?.Call("RemoveFriendList", playerID.ToString(), FactionName);

        }

        private void UpdateFactionListLM(ulong playerID, string FactionName, List<ulong> IDs)
        {
            if (!LustyMap) return;
            List<string> stringIDs = new List<string>();
            foreach (var entry in IDs)
                stringIDs.Add(entry.ToString());
            LustyMap?.Call("UpdateFriendList", playerID.ToString(), FactionName, stringIDs);
        }


        private void AddFriendLM(ulong playerID, string Name, ulong friendID)
        {
            if (!LustyMap) return;
            LustyMap?.Call("AddFriend", playerID.ToString(), Name, friendID.ToString());
        }

        private void RemoveFriendLM(ulong playerID, string Name, ulong friendID)
        {
            if (!LustyMap) return;
            LustyMap?.Call("RemoveFriend", playerID.ToString(), Name, friendID.ToString());
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
                string key = String.Empty;
                if (!string.IsNullOrEmpty(configData.MenuKeyBinding))
                    key = GetMSG("FCAltInfo", p, configData.MenuKeyBinding.ToUpper());
                GetSendMSG(p, "FCInfo", key);
            }
            timers.Add("info", timer.Once(configData.InfoInterval * 60, () => InfoLoop()));
        }

        private void ReloadPlayerPanel()
        {
            if (timers.ContainsKey("ui"))
            {
                timers["ui"].Destroy();
                timers.Remove("ui");
            }
            foreach (var player in FactionPlayers)
                PlayerPanel(player.player);
            timers.Add("ui", timer.Once(60, () => ReloadPlayerPanel()));
        }

        private static readonly DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0);
        static double CurrentTotalMinutes() => DateTime.UtcNow.Subtract(Epoch).TotalMinutes;
        #endregion

        #region Classes
        [Serializable]
        public class FactionPlayer : MonoBehaviour
        {
            public BasePlayer player;
            public ushort Faction;
            public string Panel = "HOME";
            public int page;
            public bool open;
            public bool admin;
            public bool OnlineOnly;
            public ushort SelectedFaction;
            public ulong TargetPlayer;
            void Awake()
            {
                enabled = false;
                player = GetComponent<BasePlayer>();
            }
        }

        public class FactionsData
        {
            public Dictionary<ulong, PlayerData> Players = new Dictionary<ulong, PlayerData>();
            public Dictionary<ushort, Faction> Factions = new Dictionary<ushort, Faction>();
        }

        public class Faction
        {
            public string Name;
            public string tag;
            public ulong Leader = 0L;
            public List<ulong> Moderators = new List<ulong>();
            public Dictionary<int, string> FactionAnnouncements = new Dictionary<int, string> { { 0, "Welcome to the Faction!" } };
            public bool Private;
            public string UIColor;
            public string ChatColor;
            public string KitorSet;
            public string group;
            public string description;
            public string embleem;
            public List<ulong> BanList = new List<ulong>();
            public List<ulong> factionPlayers = new List<ulong>();
            public double LastMemberLoggedIn;
            public bool AutoAuthorization;
        }

        public class PlayerData
        {
            public ushort faction = 0;
            public List<ushort> PendingInvites = new List<ushort>();
            public int RustLifeTime;
            public int RustTwoWeekTime;
        }

        class FactionDesigner
        {
            public ushort ID;
            public bool creating;
            public bool editing;
            public Faction faction;
            public int stepNum = 0;
        }
        #endregion

        #region Data Management

        void SaveData()
        {
            if (initialized)
            {
                foreach (var entry in FactionPlayers)
                    SaveFactionPlayer(entry);
                FDATA.WriteObject(fdata);
            }
        }

        void LoadData()
        {
            try
            {
                fdata = FDATA.ReadObject<FactionsData>();
                if (fdata == null || fdata.Players == null)
                    fdata = new FactionsData();
            }
            catch
            {

                Puts("Couldn't load Factions Core Data, creating new datafile");
                fdata = new FactionsData();
            }
            if (fdata.Factions == null || fdata.Factions.Count == 0)
                fdata.Factions = DefaultFactions;
            if (fdata.Players == null)
                fdata.Players = new Dictionary<ulong, PlayerData>();
        }

        private Dictionary<string, Slot> ItemSlots = new Dictionary<string, Slot>
        {
            {"tshirt", Slot.chest },
            {"tshirt.long", Slot.chest },
            {"shirt.collared", Slot.chest },
            {"shirt.tanktop", Slot.chest },
            {"jacket", Slot.chest },
            {"jacket.snow", Slot.chest },
            {"hoodie", Slot.chest },
            {"burlap.shirt", Slot.chest },
            {"hazmat.jacket", Slot.chest },

            /////
            {"wood.armor.jacket", Slot.chest2 },
            {"roadsign.jacket", Slot.chest2 },
            {"metal.plate.torso", Slot.chest2 },
            {"bone.armor.suit", Slot.chest2 },
            {"attire.hide.vest", Slot.chest2 },
            {"attire.hide.poncho", Slot.chest2 },
            {"attire.hide.helterneck", Slot.chest2 },

            /////
            {"pants", Slot.legs },
            {"pants.shorts", Slot.legs },
            {"hazmat.pants", Slot.legs },
            {"burlap.trousers", Slot.legs },
            {"attire.hide.pants", Slot.legs },
            {"attire.hide.skirt", Slot.legs },
            /////
            {"wood.armor.pants", Slot.legs2 },
            {"roadsign.kilt", Slot.legs2 },

            /////
            {"shoes.boots", Slot.feet },
            {"hazmat.boots", Slot.feet },
            {"burlap.shoes", Slot.feet },
            {"attire.hide.boots", Slot.feet },

            /////
            {"burlap.gloves", Slot.hands },
            {"hazmat.gloves", Slot.hands },

            /////
            {"mask.bandana",Slot.head },
            {"mask.balaclava",Slot.head },
            {"hat.cap",Slot.head },
            {"hat.beenie",Slot.head },
            {"bucket.helmet",Slot.head },
            {"hat.boonie",Slot.head },
            {"santahat",Slot.head },
            {"riot.helmet",Slot.head },
            {"metal.facemask",Slot.head },
            {"hazmat.helmet",Slot.head },
            {"hat.miner",Slot.head },
            {"hat.candle",Slot.head },
            {"coffeecan.helmet",Slot.head },
            {"burlap.headwrap",Slot.head },

            /////
            {"hazmatsuit", Slot.any },
        };

        enum Slot
        {
            any,
            head,
            chest,
            chest2,
            legs,
            legs2,
            feet,
            hands,
        }

        private Dictionary<int, string> DefaultEmblems = new Dictionary<int, string>
        {
            {0, "http://i.imgur.com/avnIndo.png" },
            {1, "http://i.imgur.com/bDNy4nh.png" },
            {2, "http://i.imgur.com/iwzGH4W.png" },
            {3, "http://i.imgur.com/WAnPTiw.png" },
            {4, "http://i.imgur.com/8d78Qnu.png" },
            {5, "http://i.imgur.com/gAxeMvK.png" },
            {6, "http://i.imgur.com/dNpKRwg.png" },
            {7, "http://i.imgur.com/Qu4SNm8.png" },
            {8, "http://i.imgur.com/w2IH1Eo.png" },
             {9, "http://i.imgur.com/Qzxv1zv.png" },
            {10, "http://i.imgur.com/MDi4aEn.png" },
            {11, "http://i.imgur.com/4DbaDNJ.png" },
            {12, "http://i.imgur.com/yzShlSM.png" },
            {13, "http://i.imgur.com/3HppE22.png" },
            {14, "http://i.imgur.com/Xs6QN2s.png" },
            {15, "http://i.imgur.com/NHGRtgx.png" },
            {16, "http://i.imgur.com/PeSghTc.png" },
            {17, "http://i.imgur.com/y8wo92P.png" },
            {18, "http://i.imgur.com/OhGqJbe.png" },
            {19, "http://i.imgur.com/FZk4S8s.png" },
            {20, "http://i.imgur.com/V41i65b.png" },
            {21, "http://i.imgur.com/XTwyNKE.png" },
            {22, "http://i.imgur.com/vyI8Dh0.png" },
            {23, "http://i.imgur.com/Lwpo4Uk.png" },
            {24, "http://i.imgur.com/iKs1J4i.png" },
            {25, "http://i.imgur.com/syl5cba.png" },
            {26, "http://i.imgur.com/h5fPxsP.png" },
            {27, "http://i.imgur.com/ONs75vx.png" },
            {28, "http://i.imgur.com/73UHzgO.png" },
            {29, "http://i.imgur.com/g35lupC.png" },
            {30, "http://i.imgur.com/V6x5DEd.png" },
            {31, "http://i.imgur.com/whcI2My.png" },
            {32, "http://i.imgur.com/3q7CHvO.png" },
            {33, "http://i.imgur.com/vqJIHFk.png" },
            {34, "http://i.imgur.com/wUoKvyO.png" },
            {35, "http://i.imgur.com/Oy7ZY0o.png" },
            {36, "http://i.imgur.com/SwCMLVl.png" },
            {37, "http://i.imgur.com/FpBbyJd.png" },
            {38, "http://i.imgur.com/LkOu53F.png" },
            {39, "http://i.imgur.com/NdGUphU.png" },
            {40, "http://i.imgur.com/f9iakjZ.png" },
            {41, "http://i.imgur.com/e9J33lR.png" },
            {42, "http://i.imgur.com/oK8gp0b.png" },
            {43, "http://i.imgur.com/cQ0hj5s.png" },
            {44, "http://i.imgur.com/S9iwCq8.png" },
            {45, "http://i.imgur.com/FLPXWvG.png" },
            {46, "http://i.imgur.com/uaZoI9g.png" },
            {47, "http://i.imgur.com/5mboBg8.png" },
            {48, "http://i.imgur.com/41uR2X6.png" },
            {49, "http://i.imgur.com/LhyPd3c.png" },
            {50, "http://i.imgur.com/hrw1Ti9.png" },
            {51, "http://i.imgur.com/yclMvuL.png" },
            {52, "http://i.imgur.com/ozBS4Kx.png" },
            {53, "http://i.imgur.com/lpzn12Y.png" },
            {54, "http://i.imgur.com/439Qhnh.png" },
            {55, "http://i.imgur.com/NkhVOqe.png" },
            {56, "http://i.imgur.com/zc4Pqlm.png" },
            {57, "http://i.imgur.com/JiIgA1i.png" },
            {58, "http://i.imgur.com/dzLQYMu.png" },
            {59, "http://i.imgur.com/J5eyqmq.png" },
};

        public static string HexTOUIColor(string hexColor)
        {
            if (hexColor.IndexOf('#') != -1)
                hexColor = hexColor.Replace("#", "");

            int red = 0;
            int green = 0;
            int blue = 0;
            int trans = 255;

            if (hexColor.Length == 8)
            {
                trans = int.Parse(hexColor.Substring(0, 2), NumberStyles.AllowHexSpecifier);
                red = int.Parse(hexColor.Substring(2, 2), NumberStyles.AllowHexSpecifier);
                green = int.Parse(hexColor.Substring(4, 2), NumberStyles.AllowHexSpecifier);
                blue = int.Parse(hexColor.Substring(6, 2), NumberStyles.AllowHexSpecifier);
            }

            if (hexColor.Length == 6)
            {
                red = int.Parse(hexColor.Substring(0, 2), NumberStyles.AllowHexSpecifier);
                green = int.Parse(hexColor.Substring(2, 2), NumberStyles.AllowHexSpecifier);
                blue = int.Parse(hexColor.Substring(4, 2), NumberStyles.AllowHexSpecifier);
            }
            else if (hexColor.Length == 3)
            {
                red = int.Parse(hexColor[0].ToString() + hexColor[0].ToString(), NumberStyles.AllowHexSpecifier);
                green = int.Parse(hexColor[1].ToString() + hexColor[1].ToString(), NumberStyles.AllowHexSpecifier);
                blue = int.Parse(hexColor[2].ToString() + hexColor[2].ToString(), NumberStyles.AllowHexSpecifier);
            }
            return $"{((float)red) / 255f} {((float)green) / 255f} {((float)blue) / 255f} {((float)trans) / 255f}";
        }

        private List<string> DefaultColors = new List<string>
                {
                    "#e60000",
                    "#3366ff",
                    "#29a329",
                    "#ffff00",
                    "#ff9933",
                    "#7300e6",
                    "#A93226",
                    "#4A235A",
                    "#1F618D",
                    "#117864",
                    "#D35400",
                    "#626567",
                    "#34495E",
                    "#FA0091",
                    "#3EFF00",
                    "#00E0B8",
                    "#4D8B98",
                    "#9FB1B4",
                    "#9569A8",
                    "#76125B",
                    "#161419",
                    "#3A2C09",
                    "#9ACD32",
                    "#009A9A",
                    "#523b2f",
                    "#743d1b",
                    "#d9dee2",
                    "#676862",
                    "#273a29",
        };

        private Dictionary<ushort, Faction> DefaultFactions = new Dictionary<ushort, Faction>
                {
                    {1254, new Faction
                    {
                    Name = "Faction A",
                    ChatColor = "#7300e6",
                    UIColor = "0.45 0.0 0.9 1.0",
                    group = "FactionA",
                    tag = "A",
                    embleem = "Embleem2",
                    description = "A Basic Faction Created by Default.",
                    }
                    },

                    { 1241, new Faction
                    {
                    Name = "Faction B",
                    ChatColor = "#ff9933",
                    UIColor = "1.0 0.51 0.2 1.0",
                    group = "FactionB",
                    tag = "B",
                    embleem = "Embleem9",
                    description = "A Basic Faction Created by Default.",
                    }
                    },

                    { 1287, new Faction
                    {
                    Name = "Faction C",
                    ChatColor = "#29a329",
                    UIColor = "0.16 0.63 0.16 1.0",
                    group = "FactionC",
                    tag = "C",
                    embleem = "Embleem11",
                    description = "A Basic Faction Created by Default.",
                    }
                    }
        };

        #endregion

        #region Config        
        private ConfigData configData;
        class ConfigData
        {
            public int InfoInterval { get; set; }
            public string MenuKeyBinding { get; set; }
            public bool DisableMenu { get; set; }
            public bool Use_FactionAnnouncements { get; set; }
            public bool Use_PrivateFactionChat { get; set; }
            public bool Use_FactionChatControl { get; set; }
            public bool Use_FactionTags { get; set; }
            public bool Use_PlayerListMenu { get; set; }
            public bool Use_Map { get; set; }
            public bool LockFactionKits_and_CustomSets { get; set; }
            public bool ShowFactionPlayersOnMap { get; set; }
            public int FactionLimit { get; set; }
            public int FactionPlayerLimit { get; set; }
            public int AllowedFactionDifference { get; set; }
            public bool Use_AllowPlayersToCreateFactions { get; set; }
            public bool Allow_FriendlyFire { get; set; }
            public float FriendlyFire_DamageScale { get; set; }
            public string HomePageMessage { get; set; }
            public int FactionStaleTime { get; set; }
            public bool AutoAuthorization { get; set; }
            public bool AuthorizeLeadersOnly { get; set; }
            public bool SafeZones_Allow { get; set; }
            public bool DeleteEmptyFactions { get; set; }
            public float SafeZones_Radius { get; set; }
            public string InFactionChat_ChatColor { get; set; }
            public Dictionary<int, string> FactionEmblems_URLS { get; set; }
            public List<string> Colors { get; set; }
            public List<string> Kits_and_CustomSets { get; set; }
        }
        private void LoadVariables()
        {
            LoadConfigVariables();
            if (configData == null)
                LoadDefaultConfig();
            SaveConfig();
        }
        protected override void LoadDefaultConfig()
        {
            var config = new ConfigData
            {
                MenuKeyBinding = string.Empty,
                InfoInterval = 15,
                Use_AllowPlayersToCreateFactions = true,
                HomePageMessage = "Welcome to the server! This plugin is called <color=red>FactionsCore</color> and it is written by <color=red>AbsolutRust</color> Enjoy!",
                Use_FactionChatControl = true,
                Use_PrivateFactionChat = true,
                Use_FactionAnnouncements = true,
                Use_FactionTags = true,
                Use_Map = true,
                LockFactionKits_and_CustomSets = false,
                ShowFactionPlayersOnMap = true,
                Use_PlayerListMenu = true,
                AllowedFactionDifference = 5,
                Allow_FriendlyFire = false,
                FriendlyFire_DamageScale = 0.0f,
                FactionLimit = 0,
                FactionPlayerLimit = 0,
                FactionStaleTime = 0,
                AutoAuthorization = true,
                InFactionChat_ChatColor = "#93FF7F",
                SafeZones_Allow = true,
                DeleteEmptyFactions = false,
                SafeZones_Radius = 40f,
                AuthorizeLeadersOnly = false,
                FactionEmblems_URLS = DefaultEmblems,
                DisableMenu = false,
                Colors = DefaultColors,
                Kits_and_CustomSets = new List<string> { "faction0", "faction1", "faction2", "faction3" },
            };
            SaveConfig(config);
        }
        private void LoadConfigVariables() => configData = Config.ReadObject<ConfigData>();
        void SaveConfig(ConfigData config) => Config.WriteObject(config, true);
        #endregion

        #region Messages
        Dictionary<string, string> messages = new Dictionary<string, string>()
        {
            {"title", "Factions: " },
            {"FCInfo", "This server is running Factions. Type '/faction'{0} to open the menu."},
            {"FCAltInfo", " or press '{0}'" },
            {"FFBuildings", "This is a friendly structure owned by {0}! You must be authorized on a nearby Tool Cupboard to damage it!"},
            {"FFs", "{0} is on your faction!"},
            {"FactionPlayerInfo", "{1} {0}"},
            {"AdminOptions", "Admin Menu" },
            {"ToggleProxmity", "Proximity" },
            {"OnlineList", "Online Players" },
            {"Faction", "Faction" },
            {"PlayerData", "PlayerData Info" },
            {"PlayerHealth", "Health: {0}%" },
            {"PlayerHydration", "Hydration: {0}%" },
            {"PlayerCalories", "Calories: {0}%" },
            {"CurrentLevel", "Exp Level: {0}" },
            {"FactionBegin", "To begin creating a new faction type a name for the Faction. To quit at any time simply type 'quit' or press 'n'" },
            {"FactionTag", "Please type a Faction Tag" },
            {"FactionColor", "Select a Color" },
            {"FactionShirt", "Please Select a Faction Shirt" },
            {"FactionSkin", "Please Select a Skin" },
            {"NewShirt", "Start New Shirt Selection" },
            {"FactionDescription", "Please provide a description of {0}" },
            {"CurrentDescription", "{0}..." },
            {"CreationLeader", "Leader: {0}" },
            {"CreationPlayerCount", "Player Count: {0}" },
            {"CreationDescription", "Description: {0}" },
            {"Back", "Back" },
            {"JoinFaction", "Join Faction?" },
            {"CreateFaction", "Create A Faction?" },
            {"EnterFactionChat", "You have entered Faction Chat. All message will be sent only to Faction Members." },
            {"ExitFactionChat", "You have left Faction Chat. Messages will be seen by everyone on the server." },
            {"EnterFactionAnnouncement", "You have entered Faction Announcement Mode. All message will be saved as Announcements." },
            {"ExitFactionAnnouncement", "You have left Faction Announcement Mode. Chat will work as normal" },
            {"EnterAdminView", "You have entered Admin View. Controls will now show for you as an Admin." },
            {"ExitAdminView", "You have exited Admin View. Controls will now show based on your player." },
            {"FactionOf", "Faction: {0}" },
            {"ItemCondition", "Condition: {0}%" },
            {"Magazine", "Magazine Capacity: {0}%" },
            {"CurrentWeapons", "Current Weapons" },
            {"CurrentAttire", "Current Attire" },
            {"Kills", "Kills: {0}" },
            {"Deaths", "Deaths: {0}" },
            {"FactionListEntry", "{0}\n{1}" },
            {"ONLINE", "Status: <color=#44ff44>Online</color>"},
            {"OFFLINE", "Status: <color=#ff4444>Offline</color>"},
            {"SLEEPING", "Sleeping" },
            {"NewPlayerWelcome", "{0} has joined the fight!" },
            {"PlayerReturns","{0} has returned!"},
            {"PlayerLeft", "{0} has left!" },
            {"LeftTheFaction", "{0}>{1} left {2}" },
            {"NewFactionCreated", "You have created {0}." },
            {"CreationDetails","Name:{0}\nOxide Group:{1}" },
            {"FactionDetails", "Faction Details" },
            {"SaveDescription", "Save Description?"},
            {"Continue", "Type More Description?" },
            {"SaveFaction", "Save Faction?" },
            {"LeaveFaction", "Leave Faction?" },
            {"FactionChat", "Faction Chat" },
            {"FactionJoinCooldown", "You are currently on a Faction Join Cooldown. Try again in a few minutes." },
            {"DoNotJoin", "Do Not Join" },
            {"TogglePrivate", "Private: {0}" },
            {"NoLongerPrivate", "Your Faction is no longer Private." },
            {"NowPrivate", "Your Faction is now Private." },
            {"Private", "Make Private?" },
            {"PrivateFaction", "This Faction is Private. Request an invite from {0} to join." },
            {"RemovedFromFaction", "You have been removed from {0}."},
            {"KickedPlayer", "You have removed {0} from the Faction!" },
            {"InviteMessage", "You have been invited to join the Faction: {0}. Click 'Accept' or 'Decline'" },
            {"Accept", "Accept" },
            {"Decline", "Decline" },
            {"RejectedInvite", "You have rejected the invite to join {0}" },
            {"RejectedInviteToLeader", "{0} has rejected your Faction Invite" },
            {"JoinedFaction", "{0}>{1} has joined the {2} Faction!" },
            {"Restricted", "Restricted" },
            {"AdminMenu", "Admin Menu" },
            {"NoFaction", "No Faction" },
            {"ToggleAdminView", "Admin View" },
            {"FactionTagExists", "The Faction Tag: {0} already exists. Type a different one to continue or quit to end creation" },
            {"FactionNameExists", "The Faction Name: {0} already exists. Type a different one to continue or quit to end creation" },
            {"NotAuth", "You are not authroized" },
            {"InAFaction", "You are already in a Faction." },
            {"NotALeader", "You are not a Faction Leader." },
            {"NotInAFaction", "You are not in a Faction." },
            {"SpawnCooldown", "You are on a Spawn Cooldown" },
            {"FactionSelectionTitle", "Select a Faction" },
            {"QuitFactionCreation", "You have successfully quit Faction Creation" },
            {"FactionTagToLong", "The Faction Tag:{0} is to long... must be no more then 5 characters." },
            {"ManagePlayers", "Manage Players" },
            {"ManageFactions", "Manage Factions" },
            {"DeleteFactionSafeZone", "Delete Safe Zone" },
            {"CreateFactionSafeZone", "Create Safe Zone" },
            {"InviteToFaction", "Invite To Faction" },
            {"KickFromFaction", "Kick From Faction" },
            {"AssignToFaction", "Assign To Faction" },
            {"ConfirmDelete", "Are you sure you want to delete: {0}" },
            {"DeleteFaction", "Delete: {0}" },
            {"FactionAtLimit", "{0} is at the Admin set limit of players. Choose a different Faction" },
            {"FactionToFull", "{0} has to many players. Select a different Faction" },
            {"EnterFactionSafeZone", "You have entered the {0} Safe Zone" },
            {"NoSafeZonesNearMonuments", "You can not make a Safe Zone this close to a Monument!" },
            {"SafeZoneWhereTCIS", "You can only make a Safe Zone where you have an Authorized Tool Cupboard" },
            {"FactionAnnouncements", "FACTION ANNOUNCEMENTS" },
            {"PlayTimeMinutes", "Play Time (Minutes)\nTotal: {0}\n2 Weeks: {1}" },
            {"NoPerm", "You do not have permission to use this command!" },
            {"AddModerator", "Add Moderator" },
            {"RemoveModerator", "Remove Moderator" },
            {"FactionInvite", "Invite From: {0}" },
            {"AllPlayers", "All Players" },
            {"ScoreBoard", "Scoreboard" },
            {"NotInFaction", "You are not in a faction" },
            {"WaitingImageLibrary", "Waiting on Image Library to initialize. Trying again in 60 Seconds" },
            {"MoreDetails", "Select A Player For More Details" },
            {"MoreDetails-OnlineOnly", "Select A Player For More Details - Showing Online Only" },
            {"ToggleOnlineOnly", "Toggle Online/All" },
            {"InvitePlayers", "Invite Players" },
            {"SetTaxBox", "Set Tax Box" },
            {"KitContents", "{0} Kit Contents" },
            {"OnlineOnly", "{0} - Online Only" },
            {"AllMembers", "{0} - All Members" },
            ///OPTIONS MESSAGES
            {"OptionChangeMSG", "Do you want to change this setting to: {0}" },
            {"OptionChangeTitle", "{0} is currently set to: {1}" },
            {"TRUE", "<color=#005800>TRUE</color>" },
            {"FALSE", "<color=#FF0000>FALSE</color>" },
            {"ChatControlTitle", "Factions Chat Control" },
            {"ChatControlInfo", "This setting controls the use of Faction colors and attributes when players send a message. This setting has no impact on the ability to Toggle Faction Chat for private internal faction communication." },
            {"FactionTagOnChatTitle", "Faction Tag" },
            {"FactionTagOnChatInfo", "This setting controls the addition of the Faction Tag on Faction Member chat messages. For Example: [TAG]PLAYERNAME: MSG… If this setting is set to ‘TRUE’ it will automatically set Faction Chat Control to ‘TRUE’ as it is a requirement. This can also be used with ChatTitles." },
            {"FriendlyFireTitle", "Friendly Fire" },
            {"FriendlyFireInfo", "This setting controls whether Faction Members and Faction Member Buildings can be damaged by Friendly Fire. If this setting is set to ‘FALSE’ players within Factions will not be able to damage eachother or structures (unless Authorized on a ToolCupboard). ‘FriendlyFire_DamageScale’ can be changed in the Config File to determine how much damage is done even if Friendly Fire Protection is set to ‘FALSE’. The default setting for ‘FriendlyFire_DamageScale’ is ‘0’. " },
            {"AutoAuthorizationInfo", "This setting controls whether players within a faction are authorized on Turrets and Doors automatically. If set as ‘true’ whenever a turret is made all faction players will be authorized on it automatically. Additionally, if a player tries to open a faction member door and is authorized on a nearby turret they will be able to open the door without a key or key code." },
            {"AutoAuthorizationTitle", "Authorization" },
            {"AuthorizeLeadersOnlyInfo", "This setting modifies the default Authorization settings to only include the leader of each faction instead of all faction members." },
            {"AuthorizeLeadersOnlyTitle", "Authorize Leaders Only" },
            {"SafeZones_AllowInfo", "This setting controls whether leaders and moderators of a faction can create a Safe Zone. Only members of the faction can enter the safe zone. There is a Safe Zone radius setting in the config file to modify how big the zone will be. Additionally, if you have the plugin ZoneDomes loaded it will shade the zone with a dome." },
            {"SafeZones_AllowTitle", "Safe Zones" },
            {"DeleteEmptyFactionsInfo", "Controls whether a faction with no players will automatically be deleted. The check for empty factions happens when a player leaves the faction." },
            {"DeleteEmptyFactionsTitle", "Delete Empty Factions" },
            {"DisableMenuInfo", "Controls whether the UI menu is usable by players. Disabling the menu will still allow it to be used by players with Auth2 or with the FactionsCore.admin permission." },
            {"DisableMenuTitle", "Disable UI Menu" },
            {"Use_FactionAnnouncementsInfo", "This setting controls whether faction leaders and moderators can make Faction Announcements. These announcements are shown in the UI Menu for faction members. This also enables/disables the Faction Announcement toggle button below the chat menu." },
            {"Use_FactionAnnouncementsTitle", "Faction Announcements" },
            {"Use_PrivateFactionChatInfo", "This setting controls whether factions can use the Private Chat function which allows players to chat in a private conversation with only members of the faction. This also enables/disables the Faction Chat toggle button below the chat menu." },
            {"Use_PrivateFactionChatTitle", "Private Chat" },
            {"LockFactionKits_and_CustomSetsInfo", "This setting controls whether faction provided kits and Custom Sets are locked to the player. If set to ‘true’ players will not be able to remove any item given to them through the kit or set." },
            {"LockFactionKits_and_CustomSetsTitle", "Lock Faction Gear" },
            {"Use_PlayerListMenuInfo", "This setting controls whether players can use the ‘All Players’ tab in the UI Menu." },
            {"Use_PlayerListMenuTitle", "Player List" },
            {"ShowFactionPlayersOnMapInfo", "This setting controls whether players can see faction members on the built in UI Map." },
            {"ShowFactionPlayersOnMapTitle", "Show Players on Map" },
            {"PlayersToCreateFactionsInfo", "This setting controls whether players can create their own factions." },
            {"PlayersToCreateFactionsTitle", "Player Faction Creation" },
            {"MapDisabled", "The Map is disabled!" },
        };
        #endregion
    }
}