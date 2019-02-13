using System;
using System.Text;
using System.Collections.Generic;
using UnityEngine;
using Oxide.Core;
using Oxide.Core.Plugins;
using Newtonsoft.Json;
using System.Linq;

namespace Oxide.Plugins
{
    [Info ("Trade", "Calytic", "1.2.4")]
    class Trade : RustPlugin
    {
        #region Configuration

        string box;
        int slots;
        float cooldownMinutes;
        float maxRadius;
        float pendingSeconds;
        float radiationMax;
        bool allowSafeZone;

        [PluginReference]
        Plugin Ignore, Clans, Friends;

        Dictionary<string, DateTime> tradeCooldowns = new Dictionary<string, DateTime> ();

        #endregion

        #region Trade State

        class OnlinePlayer
        {
            public BasePlayer Player;
            public StorageContainer View;
            public OpenTrade Trade;

            public PlayerInventory inventory {
                get {
                    return Player.inventory;
                }
            }

            public ItemContainer containerMain {
                get {
                    return Player.inventory.containerMain;
                }
            }

            public OnlinePlayer (BasePlayer player)
            {
            }

            public void Clear ()
            {
                View = null;
                Trade = null;
            }
        }

        [OnlinePlayers]
        Hash<BasePlayer, OnlinePlayer> onlinePlayers = new Hash<BasePlayer, OnlinePlayer> ();

        class OpenTrade
        {
            public OnlinePlayer source;
            public OnlinePlayer target;

            public BasePlayer sourcePlayer {
                get {
                    return source.Player;
                }
            }

            public BasePlayer targetPlayer {
                get {
                    return target.Player;
                }
            }

            public bool complete = false;
            public bool closing = false;

            public bool sourceAccept = false;
            public bool targetAccept = false;

            public OpenTrade (OnlinePlayer source, OnlinePlayer target)
            {
                this.source = source;
                this.target = target;
            }

            public OnlinePlayer GetOther (OnlinePlayer onlinePlayer)
            {
                if (source == onlinePlayer) {
                    return target;
                }

                return source;
            }

            public BasePlayer GetOther (BasePlayer player)
            {
                if (sourcePlayer == player) {
                    return targetPlayer;
                }

                return sourcePlayer;
            }

            public void ResetAcceptance ()
            {
                sourceAccept = false;
                targetAccept = false;
            }

            public bool IsInventorySufficient ()
            {
                if (target == null || source == null) {
                    return false;
                }

                if (target.containerMain == null || source.containerMain == null) {
                    return false;
                }

                if ((target.containerMain.capacity - target.containerMain.itemList.Count) < source.View.inventory.itemList.Count ||
                       (source.containerMain.capacity - source.containerMain.itemList.Count) < target.View.inventory.itemList.Count) {
                    return true;
                }

                return false;
            }

            public bool IsValid ()
            {
                if (IsSourceValid () && IsTargetValid ())
                    return true;

                return false;
            }

            public bool IsSourceValid ()
            {
                if (sourcePlayer != null && sourcePlayer.IsConnected)
                    return true;

                return false;
            }

            public bool IsTargetValid ()
            {
                if (targetPlayer != null && targetPlayer.IsConnected)
                    return true;

                return false;
            }
        }

        class PendingTrade
        {
            public BasePlayer Target;
            public Timer Timer;

            public PendingTrade (BasePlayer target)
            {
                Target = target;
            }

            public void Destroy ()
            {
                if (Timer != null && !Timer.Destroyed) {
                    Timer.Destroy ();
                }
            }
        }

        List<OpenTrade> openTrades = new List<OpenTrade> ();
        Dictionary<BasePlayer, PendingTrade> pendingTrades = new Dictionary<BasePlayer, PendingTrade> ();
        #endregion

        #region Initialization

        void Init ()
        {

            UnsubscribeAll ();
        }

        void UnsubscribeAll ()
        {
            //Unsubscribe(nameof(CanNetworkTo));
            Unsubscribe (nameof (OnItemAction));
            Unsubscribe (nameof (OnItemAddedToContainer));
            Unsubscribe (nameof (CanMoveItem));
            Unsubscribe (nameof (OnItemRemovedFromContainer));
        }

        void SubscribeAll ()
        {
            //Subscribe(nameof(CanNetworkTo));
            Subscribe (nameof (OnItemAction));
            Subscribe (nameof (OnItemAddedToContainer));
            Subscribe (nameof (CanMoveItem));
            Subscribe (nameof (OnItemRemovedFromContainer));
        }

        void Loaded ()
        {
            permission.RegisterPermission ("trade.use", this);
            permission.RegisterPermission ("trade.accept", this);

            LoadMessages ();

            CheckConfig ();

            box = GetConfig ("Settings", "box", "assets/prefabs/deployable/woodenbox/woodbox_deployed.prefab");
            slots = GetConfig ("Settings", "slots", 30);
            cooldownMinutes = GetConfig ("Settings", "cooldownMinutes", 5f);
            maxRadius = GetConfig ("Settings", "maxRadius", 5000f);
            pendingSeconds = GetConfig ("Settings", "pendingSeconds", 25f);
            radiationMax = GetConfig ("Settings", "radiationMax", 1f);
            allowSafeZone = GetConfig ("Settings", "allowSafeZone", true);
        }

        void Unloaded ()
        {
            foreach (var player in BasePlayer.activePlayerList) {
                OnlinePlayer onlinePlayer;
                if (onlinePlayers.TryGetValue (player, out onlinePlayer)) {
                    if (onlinePlayer.Trade != null) {
                        TradeCloseBoxes (onlinePlayer.Trade);
                    } else if (onlinePlayer.View != null) {
                        CloseBoxView (player, onlinePlayer.View);
                    }
                }
            }
        }

        protected new void LoadDefaultConfig ()
        {
            Config ["Settings", "box"] = "assets/prefabs/deployable/woodenbox/woodbox_deployed.prefab";
            Config ["Settings", "slots"] = 30;
            Config ["Settings", "cooldownMinutes"] = 5;
            Config ["Settings", "maxRadius"] = 5000f;
            Config ["Settings", "pendingSeconds"] = 25f;
            Config ["Settings", "radiationMax"] = 1;
            Config ["Settings", "allowSafeZone"] = true;
            Config ["VERSION"] = Version.ToString ();
        }

        void CheckConfig ()
        {
            if (Config ["VERSION"] == null) {
                // FOR COMPATIBILITY WITH INITIAL VERSIONS WITHOUT VERSIONED CONFIG
                ReloadConfig ();
            } else if (GetConfig<string> ("VERSION", "") != Version.ToString ()) {
                // ADDS NEW, IF ANY, CONFIGURATION OPTIONS
                ReloadConfig ();
            }
        }

        protected void ReloadConfig ()
        {
            Config ["VERSION"] = Version.ToString ();

            // NEW CONFIGURATION OPTIONS HERE
            Config ["Settings", "radiationMax"] = GetConfig ("Settings", "radiationMax", 1f);
            // END NEW CONFIGURATION OPTIONS

            PrintToConsole ("Upgrading configuration file");
            SaveConfig ();
        }

        void LoadMessages ()
        {
            lang.RegisterMessages (new Dictionary<string, string>
            {
                {"Inventory: You", "You do not have enough room in your inventory"},
                {"Inventory: Them", "Their inventory does not have enough room"},
                {"Inventory: Generic", "Insufficient inventory space"},

                {"Player: Not Found", "No player found by that name"},
                {"Player: Unknown", "Unknown"},
                {"Player: Yourself", "You cannot trade with yourself"},

                {"Status: Completing", "Completing trade.."},
                {"Status: No Pending", "You have no pending trade requests"},
                {"Status: Pending", "They already have a pending trade request"},
                {"Status: Received", "You have received a trade request from {0}. Type <color=lime>/trade accept</color> to begin trading"},
                {"Status: They Interrupted", "They moved or closed the trade"},
                {"Status: You Interrupted", "You moved or closed the trade"},

                {"Trade: Sent", "Trade request sent"},
                {"Trade: They Declined", "They declined your trade request"},
                {"Trade: You Declined", "You declined their trade request"},
                {"Trade: They Accepted", "{0} accepted."},
                {"Trade: You Accepted", "You accepted."},
                {"Trade: Pending", "Trade pending."},

                {"Denied: Permission", "You lack permission to do that"},
                {"Denied: Privilege", "You do no have building privilege"},
                {"Denied: Swimming", "You cannot do that while swimming"},
                {"Denied: Falling", "You cannot do that while falling"},
                {"Denied: Mounted", "You cannot do that while mounted"},
                {"Denied: Wounded", "You cannot do that while wounded"},
                {"Denied: Irradiated", "You cannot do that while irradiated"},
                {"Denied: Generic", "You cannot do that right now"},
                {"Denied: They Busy", "That player is busy"},
                {"Denied: They Ignored You", "They ignored you"},
                {"Denied: Distance", "Too far away"},
                {"Denied: Ship", "You cannot do that while on a ship"},
                {"Denied: Lift", "You cannot do that while on a lift"},
                {"Denied: Balloon", "You cannot do that while on a balloon"},
                {"Denied: Safe Zone", "You cannot do that while in a safe zone"},

                {"Item: BP", "BP"},

                {"Syntax: Trade Accept", "Invalid syntax. /trade accept"},
                {"Syntax: Trade", "Invalid syntax. /trade \"Player Name\""},

                {"Cooldown: Seconds", "You are doing that too often, try again in a {0} seconds(s)."},
                {"Cooldown: Minutes", "You are doing that too often, try again in a {0} minute(s)."},
            }, this);
        }

        #endregion

        #region Oxide Hooks

        //object CanNetworkTo(BaseNetworkable entity, BasePlayer target)
        //{
        //    if (entity == null || target == null || entity == target) return null;
        //    if (target.IsAdmin) return null;

        //    OnlinePlayer onlinePlayer;
        //    bool IsMyBox = false;
        //    if (onlinePlayers.TryGetValue(target, out onlinePlayer))
        //    {
        //        if (onlinePlayer.View != null && onlinePlayer.View.net.ID == entity.net.ID)
        //        {
        //            IsMyBox = true;
        //        }
        //    }

        //    if (IsTradeBox(entity) && !IsMyBox) return false;

        //    return null;
        //}

        void OnPlayerInit (BasePlayer player)
        {
            onlinePlayers [player].View = null;
            onlinePlayers [player].Trade = null;
        }

        void OnPlayerDisconnected (BasePlayer player)
        {
            OnlinePlayer onlinePlayer;
            if (onlinePlayers.TryGetValue (player, out onlinePlayer)) {
                if (onlinePlayer.Trade != null) {
                    TradeCloseBoxes (onlinePlayer.Trade);
                } else if (onlinePlayer.View != null) {
                    CloseBoxView (player, onlinePlayer.View);
                }
            }
        }

        void OnPlayerLootEnd (PlayerLoot inventory)
        {
            var player = inventory.GetComponent<BasePlayer> ();
            if (player == null)
                return;

            OnlinePlayer onlinePlayer;
            if (onlinePlayers.TryGetValue (player, out onlinePlayer) && onlinePlayer.View != null) {
                if (onlinePlayer.View == inventory.entitySource && onlinePlayer.Trade != null) {
                    OpenTrade t = onlinePlayer.Trade;

                    if (!t.closing) {
                        t.closing = true;
                        if (!onlinePlayer.Trade.complete) {
                            if (onlinePlayer.Trade.sourcePlayer == player) {
                                TradeReply (t, "Status: They Interrupted", "Status: You Interrupted");
                            } else {
                                TradeReply (t, "Status: You Interrupted", "Status: They Interrupted");
                            }
                        }
                        CloseBoxView (player, (StorageContainer)inventory.entitySource);
                    }
                }
            }
        }

        void OnItemAction (Item item, string cmd)
        {
            if (cmd == "drop") {
                BasePlayer player = item.GetOwnerPlayer ();

                if (player is BasePlayer) {
                    OnlinePlayer onlinePlayer;
                    if (onlinePlayers.TryGetValue (player, out onlinePlayer) && onlinePlayer.Trade != null && player.inventory != null) {
                        if (item.parent == player.inventory.containerMain && !onlinePlayer.Trade.IsInventorySufficient ()) {
                            ShowTrades (onlinePlayer.Trade, "Trade: Pending");
                        }
                    }
                }
            }
        }

        void OnItemAddedToContainer (ItemContainer container, Item item)
        {
            if (container.playerOwner is BasePlayer) {
                OnlinePlayer onlinePlayer;
                if (onlinePlayers.TryGetValue (container.playerOwner, out onlinePlayer) && onlinePlayer.Trade != null) {
                    OpenTrade t = onlinePlayers [container.playerOwner].Trade;

                    if (!t.complete) {
                        t.ResetAcceptance ();

                        if (t.IsValid ()) {
                            ShowTrades (t, "Trade: Pending");
                        } else {
                            TradeCloseBoxes (t);
                        }
                    }
                }
            }
        }

        object CanMoveItem (Item item, PlayerInventory playerLoot, uint targetContainer, int targetSlot)
        {
            var player = playerLoot.GetComponent<BasePlayer> ();
            if (player == null) {
                return null;
            }

            OnlinePlayer onlinePlayer;
            if (onlinePlayers.TryGetValue (player, out onlinePlayer) && onlinePlayer.Trade != null) {
                OpenTrade t = onlinePlayers [player].Trade;
                if (t.closing) {
                    return false;
                }
            }

            return null;
        }

        void OnItemRemovedFromContainer (ItemContainer container, Item item)
        {
            if (container.playerOwner is BasePlayer) {
                OnlinePlayer onlinePlayer;
                if (onlinePlayers.TryGetValue (container.playerOwner, out onlinePlayer) && onlinePlayer.Trade != null) {
                    OpenTrade t = onlinePlayers [container.playerOwner].Trade;
                    if (!t.complete) {
                        t.ResetAcceptance ();

                        if (t.IsValid ()) {
                            ShowTrades (t, "Trade: Pending");
                        } else {
                            TradeCloseBoxes (t);
                        }
                    }
                }
            }
        }

        #endregion

        #region Commands

        [ChatCommand ("trade")]
        void cmdTrade (BasePlayer player, string command, string [] args)
        {
            if (args.Length == 1) {
                if (args [0] == "accept") {
                    if (!CanPlayerTrade (player, "trade.accept"))
                        return;

                    AcceptTrade (player);
                    return;
                }
            }

            if (args.Length != 1) {
                if (pendingTrades.ContainsKey (player)) {
                    SendReply (player, GetMsg ("Syntax: Trade Accept", player));
                } else {
                    SendReply (player, GetMsg ("Syntax: Trade", player));
                }

                return;
            }

            var targetPlayer = FindPlayerByPartialName (args [0]);
            if (targetPlayer == null) {
                SendReply (player, GetMsg ("Player: Not Found", player));
                return;
            }

            if (targetPlayer == player) {
                SendReply (player, GetMsg ("Player: Yourself", player));
                return;
            }

            if (!CheckCooldown (player)) {
                return;
            }

            if (Ignore != null) {
                var IsIgnored = Ignore.Call ("IsIgnoredS", player.UserIDString, targetPlayer.UserIDString);
                if ((bool)IsIgnored == true) {
                    SendReply (player, GetMsg ("Denied: They Ignored You", player));
                    return;
                }
            }

            OnlinePlayer onlineTargetPlayer;
            if (onlinePlayers.TryGetValue (targetPlayer, out onlineTargetPlayer) && onlineTargetPlayer.Trade != null) {
                SendReply (player, GetMsg ("Denied: They Busy", player));
                return;
            }

            if (maxRadius > 0) {
                if (targetPlayer.Distance (player) > maxRadius) {
                    SendReply (player, GetMsg ("Denied: Distance", player));
                    return;
                }
            }

            if (!CanPlayerTrade (player, "trade.use"))
                return;

            if (pendingTrades.ContainsKey (player)) {
                SendReply (player, GetMsg ("Status: Pending", player));
            } else {
                SendReply (targetPlayer, GetMsg ("Status: Received", targetPlayer), player.displayName);
                SendReply (player, GetMsg ("Trade: Sent", player));
                var pendingTrade = new PendingTrade (targetPlayer);
                pendingTrades.Add (player, pendingTrade);

                pendingTrade.Timer = timer.In (pendingSeconds, delegate () {
                    if (pendingTrades.ContainsKey (player)) {
                        pendingTrades.Remove (player);
                        SendReply (player, GetMsg ("Trade: They Declined", player));
                        SendReply (targetPlayer, GetMsg ("Trade: You Declined", targetPlayer));
                    }
                });
            }
        }

        [ConsoleCommand ("trade")]
        void ccTrade (ConsoleSystem.Arg arg)
        {
            if (arg.Connection == null)
                return;
            if (arg.Connection.player == null)
                return;
            cmdTrade (arg.Connection.player as BasePlayer, arg.cmd.Name, arg.Args);
        }

        [ConsoleCommand ("trade.decline")]
        void ccTradeDecline (ConsoleSystem.Arg arg)
        {
            if (arg.Connection == null)
                return;
            if (arg.Connection.player == null)
                return;
            var player = arg.Connection.player as BasePlayer;

            OnlinePlayer onlinePlayer;
            if (onlinePlayers.TryGetValue (player, out onlinePlayer) && onlinePlayer.Trade != null) {
                onlinePlayer.Trade.closing = true;
                var target = onlinePlayer.Trade.GetOther (player);
                SendReply (player, GetMsg ("Trade: You Declined", player));
                SendReply (target, GetMsg ("Trade: They Declined", target));

                TradeCloseBoxes (onlinePlayer.Trade);
            } else if (player is BasePlayer) {
                HideTrade (player);
            }
        }

        [ConsoleCommand ("trade.accept")]
        void ccTradeAccept (ConsoleSystem.Arg arg)
        {
            if (arg.Connection == null)
                return;
            if (arg.Connection.player == null)
                return;
            var player = arg.Connection.player as BasePlayer;

            TradeAccept (player);
        }

        void TradeAccept (BasePlayer player)
        {
            OnlinePlayer onlinePlayer;
            if (onlinePlayers.TryGetValue (player, out onlinePlayer) && onlinePlayer.Trade != null) {
                var t = onlinePlayers [player].Trade;
                if (t.sourcePlayer == player) {
                    if (!CheckSourceInventory (t)) {
                        return;
                    }

                    t.sourceAccept = true;
                } else if (t.targetPlayer == player) {
                    if (!CheckTargetInventory (t)) {
                        return;
                    }

                    t.targetAccept = true;
                }

                if (t.targetAccept == true && t.sourceAccept == true) {
                    CompleteTrade (t);
                } else {
                    ShowTrades (t, "Trade: Pending");
                }
            } else if (player is BasePlayer) {
                HideTrade (player);
            }
        }

        void CompleteTrade (OpenTrade t)
        {
            if (t.IsInventorySufficient ()) {
                t.ResetAcceptance ();
                ShowTrades (t, "Inventory: Generic");
                return;
            }
            if (t.complete) {
                return;
            }
            t.complete = true;
            t.closing = true;

            TradeCooldown (t);

            TradeReply (t, "Status: Completing");
            Interface.Oxide.NextTick (() => FinishTrade (t));
        }

        bool CheckSourceInventory (OpenTrade t)
        {
            var i = t.target.View.inventory.itemList.Count;
            var f = t.source.containerMain.capacity - t.source.containerMain.itemList.Count;
            if (i > f) {

                TradeReply (t, "Inventory: Them", "Inventory: You");

                t.sourceAccept = false;
                ShowTrades (t, "Inventory: Generic");
                return false;
            }

            return true;
        }

        bool CheckTargetInventory (OpenTrade t)
        {
            var i = t.source.View.inventory.itemList.Count;
            var f = t.target.containerMain.capacity - t.target.containerMain.itemList.Count;
            if (i > f) {
                TradeReply (t, "Inventory: You", "Inventory: Them");
                t.targetAccept = false;
                ShowTrades (t, "Inventory: Generic");
                return false;
            }

            return true;
        }

        #endregion

        #region GUI

        public string jsonTrade = @"[{""name"":""TradeMsg"",""parent"":""Overlay"",""components"":[{""type"":""UnityEngine.UI.Image"",""color"":""0 0 0 0.76"",""imagetype"":""Filled""},{""type"":""RectTransform"",""anchormax"":""0.77 0.91"",""anchormin"":""0.24 0.52""}]},{""name"":""SourceLabel{1}"",""parent"":""TradeMsg"",""components"":[{""type"":""UnityEngine.UI.Text"",""text"":""{sourcename}"",""fontSize"":""16"",""align"":""UpperLeft""},{""type"":""RectTransform"",""anchormax"":""0.48 0.98"",""anchormin"":""0.03 0.91""}]},{""name"":""TargetLabel{2}"",""parent"":""TradeMsg"",""components"":[{""type"":""UnityEngine.UI.Text"",""text"":""{targetname}"",""fontSize"":""17""},{""type"":""RectTransform"",""anchormax"":""0.97 0.98"",""anchormin"":""0.52 0.91""}]},{""name"":""SourceItemsPanel{3}"",""parent"":""TradeMsg"",""components"":[{""type"":""UnityEngine.UI.RawImage"",""color"":""0 0 0 0.52"",""imagetype"":""Filled""},{""type"":""RectTransform"",""anchormax"":""0.47 0.9"",""anchormin"":""0.03 0.13""}]},{""name"":""SourceItemsText"",""parent"":""SourceItemsPanel{3}"",""components"":[{""type"":""UnityEngine.UI.Text"",""text"":""{sourceitems}"",""fontSize"":""14"",""align"":""UpperLeft""},{""type"":""RectTransform"",""anchormax"":""0.99 0.99"",""anchormin"":""0.01 0.01""}]},{""name"":""TargetItemsPanel{4}"",""parent"":""TradeMsg"",""components"":[{""type"":""UnityEngine.UI.RawImage"",""color"":""0 0 0 0.52"",""imagetype"":""Filled""},{""type"":""RectTransform"",""anchormax"":""0.96 0.9"",""anchormin"":""0.52 0.13""}]},{""name"":""TargetItemsText"",""parent"":""TargetItemsPanel{4}"",""components"":[{""type"":""UnityEngine.UI.Text"",""text"":""{targetitems}"",""fontSize"":""14"",""align"":""UpperLeft""},{""type"":""RectTransform"",""anchormax"":""0.99 0.99"",""anchormin"":""0.01 0.01""}]},{""name"":""AcceptTradeButton{5}"",""parent"":""TradeMsg"",""components"":[{""type"":""UnityEngine.UI.Button"",""color"":""0 0.95 0.14 0.54"",""command"":""trade.accept""},{""type"":""RectTransform"",""anchormax"":""0.47 0.09"",""anchormin"":""0.35 0.03""}]},{""name"":""AcceptTradeLabel"",""parent"":""AcceptTradeButton{5}"",""components"":[{""type"":""UnityEngine.UI.Text"",""text"":""Accept"",""fontSize"":""13"",""align"":""MiddleCenter""},{""type"":""RectTransform"",""anchormax"":""1 1"",""anchormin"":""0 0""}]},{""name"":""DeclineTradeButton{6}"",""parent"":""TradeMsg"",""components"":[{""type"":""UnityEngine.UI.Button"",""color"":""0.95 0 0.02 0.61"",""command"":""trade.decline""},{""type"":""RectTransform"",""anchormax"":""0.15 0.09"",""anchormin"":""0.03 0.03""}]},{""name"":""DeclineTradeLabel"",""parent"":""DeclineTradeButton{6}"",""components"":[{""type"":""UnityEngine.UI.Text"",""text"":""Decline"",""fontSize"":""13"",""align"":""MiddleCenter""},{""type"":""RectTransform"",""anchormax"":""1 1"",""anchormin"":""0 0""}]},{""name"":""TargetStatusLabel{7}"",""parent"":""TradeMsg"",""components"":[{""type"":""UnityEngine.UI.Text"",""text"":""{targetstatus}"",""fontSize"":""14"",""align"":""UpperLeft""},{""type"":""RectTransform"",""anchormax"":""0.97 0.09"",""anchormin"":""0.52 0.01""}]}]
";
        private void ShowTrade (BasePlayer player, OpenTrade trade, string status)
        {
            HideTrade (player);

            OnlinePlayer onlinePlayer;
            if (!onlinePlayers.TryGetValue (player, out onlinePlayer)) {
                return;
            }

            if (onlinePlayer.View == null) {
                return;
            }

            StorageContainer sourceContainer = onlinePlayer.View;
            StorageContainer targetContainer = null;
            BasePlayer target = null;

            if (trade.sourcePlayer == player && trade.target.View != null) {
                targetContainer = trade.target.View;
                target = trade.targetPlayer;
                if (target is BasePlayer) {
                    if (trade.targetAccept) {
                        status += string.Format (GetMsg ("Trade: They Accepted", player), CleanName (target.displayName));
                    } else if (trade.sourceAccept) {
                        status += GetMsg ("Trade: You Accepted", player);
                    }
                } else {
                    return;
                }
            } else if (trade.targetPlayer == player && trade.source.View != null) {
                targetContainer = trade.source.View;
                target = trade.sourcePlayer;
                if (target is BasePlayer) {
                    if (trade.sourceAccept) {
                        status += string.Format (GetMsg ("Trade: They Accepted", player), CleanName (target.displayName));
                    } else if (trade.targetAccept) {
                        status += GetMsg ("Trade: You Accepted", player);
                    }
                } else {
                    return;
                }
            }

            if (targetContainer == null || target == null) {
                return;
            }

            string send = jsonTrade;
            for (int i = 1; i < 100; i++) {
                send = send.Replace ("{" + i + "}", Oxide.Core.Random.Range (9999, 99999).ToString ());
            }

            send = send.Replace ("{sourcename}", CleanName (player.displayName));
            if (target != null) {
                send = send.Replace ("{targetname}", CleanName (target.displayName));
            } else {
                send = send.Replace ("{targetname}", GetMsg ("Player: Unknown", player));
            }
            send = send.Replace ("{targetstatus}", status);

            var slotsAvailable = target.inventory.containerMain.capacity - (target.inventory.containerMain.itemList.Count);
            List<string> sourceItems = new List<string> ();
            var x = 1;
            foreach (Item i in sourceContainer.inventory.itemList) {
                string n = "";
                if (i.IsBlueprint ()) {
                    n = i.amount + " x <color=lightblue>" + i.blueprintTargetDef.displayName.english + " [" + GetMsg ("Item: BP", player) + "]</color>";
                } else {
                    n = i.amount + " x " + i.info.displayName.english;
                    if(i.info.condition.enabled)
                    {
                        var conditionPercent = System.Math.Round(i.condition * 100 / i.info.condition.max, 0);
                        if(conditionPercent <= 25f)
                        {
                            n += " [<color=red>" + conditionPercent + "%</color>]";
                        }
                        else if(conditionPercent <= 75f)
                        {
                            n += " [<color=yellow>" + conditionPercent + "%</color>]";
                        }
                        else if(conditionPercent <= 99f)
                        {
                            n += " [<color=green>" + conditionPercent + "%</color>]";
                        }
                    }
                }

                if (x > slotsAvailable) {
                    n = "<color=red>" + n + "</color>";
                }
                x++;

                sourceItems.Add (n);
            }

            send = send.Replace ("{sourceitems}", string.Join ("\n", sourceItems.ToArray ()));

            if (player != target) {
                slotsAvailable = player.inventory.containerMain.capacity - (player.inventory.containerMain.itemList.Count);
                List<string> targetItems = new List<string> ();
                x = 1;
                if (targetContainer != null) {
                    foreach (Item i in targetContainer.inventory.itemList) {
                        string n2 = "";
                        if (i.IsBlueprint ()) {
                            n2 = i.amount + " x <color=lightblue>" + i.blueprintTargetDef.displayName.english + " [" + GetMsg ("Item: BP", player) + "]</color>";
                        } else {
                            n2 = i.amount + " x " + i.info.displayName.english;
                            if (i.info.condition.enabled)
                            {
                                var conditionPercent = System.Math.Round(i.condition * 100 / i.info.condition.max, 0);
                                if (conditionPercent <= 25f)
                                {
                                    n2 += " [<color=red>" + conditionPercent + "%</color>]";
                                }
                                else if (conditionPercent <= 75f)
                                {
                                    n2 += " [<color=yellow>" + conditionPercent + "%</color>]";
                                }
                                else if (conditionPercent <= 99f)
                                {
                                    n2 += " [<color=green>" + conditionPercent + "%</color>]";
                                }
                            }
                        }
                        if (x > slotsAvailable) {
                            n2 = "<color=red>" + n2 + "</color>";
                        }
                        x++;
                        targetItems.Add (n2);
                    }
                }

                send = send.Replace ("{targetitems}", string.Join ("\n", targetItems.ToArray ()));
            } else {
                send = send.Replace ("{targetitems}", "");
            }

            CommunityEntity.ServerInstance.ClientRPCEx (new Network.SendInfo { connection = player.net.connection }, null, "AddUI", send);
        }

        private void HideTrade (BasePlayer player)
        {
            if (player.IsConnected) {
                CommunityEntity.ServerInstance.ClientRPCEx (new Network.SendInfo { connection = player.net.connection }, null, "DestroyUI", "TradeMsg");
            }
        }

        #endregion

        #region Core Methods

        bool CheckCooldown (BasePlayer player)
        {
            if (cooldownMinutes > 0) {
                DateTime startTime;
                if (tradeCooldowns.TryGetValue (player.UserIDString, out startTime)) {
                    var endTime = DateTime.Now;

                    var span = endTime.Subtract (startTime);
                    if (span.TotalMinutes > 0 && span.TotalMinutes < Convert.ToDouble (cooldownMinutes)) {
                        double timeleft = System.Math.Round (Convert.ToDouble (cooldownMinutes) - span.TotalMinutes, 2);
                        if (timeleft < 1) {
                            double timelefts = System.Math.Round ((Convert.ToDouble (cooldownMinutes) * 60) - span.TotalSeconds);
                            SendReply (player, string.Format (GetMsg ("Cooldown: Seconds", player), timelefts.ToString ()));
                        } else {
                            SendReply (player, string.Format (GetMsg ("Cooldown: Minutes", player), System.Math.Round (timeleft).ToString ()));
                        }
                        return false;
                    } else {
                        tradeCooldowns.Remove (player.UserIDString);
                    }
                }
            }

            return true;
        }

        void TradeCloseBoxes (OpenTrade trade)
        {
            if (trade.IsSourceValid ()) {
                CloseBoxView (trade.sourcePlayer, trade.source.View);
            }

            if (trade.IsTargetValid () && trade.targetPlayer != trade.sourcePlayer) {
                CloseBoxView (trade.targetPlayer, trade.target.View);
            }
        }

        void TradeReply (OpenTrade trade, string msg, string msg2 = null)
        {
            if (msg2 == null) {
                msg2 = msg;
            }

            if (trade.targetPlayer != null) {
                SendReply (trade.targetPlayer, GetMsg (msg, trade.targetPlayer));
            }

            if (trade.sourcePlayer != null) {
                SendReply (trade.sourcePlayer, GetMsg (msg2, trade.sourcePlayer));
            }
        }

        void ShowTrades (OpenTrade trade, string msg)
        {
            ShowTrade (trade.sourcePlayer, trade, GetMsg (msg, trade.sourcePlayer));
            ShowTrade (trade.targetPlayer, trade, GetMsg (msg, trade.targetPlayer));
        }

        void TradeCooldown (OpenTrade trade)
        {
            PlayerCooldown (trade.targetPlayer);
            PlayerCooldown (trade.sourcePlayer);
        }

        void PlayerCooldown (BasePlayer player)
        {
            if (player.IsAdmin) {
                return;
            }
            if (tradeCooldowns.ContainsKey (player.UserIDString)) {
                tradeCooldowns.Remove (player.UserIDString);
            }

            tradeCooldowns.Add (player.UserIDString, DateTime.Now);
        }

        void FinishTrade (OpenTrade t)
        {
            foreach (var i in t.source.View.inventory.itemList.ToArray ()) {
                i.MoveToContainer (t.target.containerMain);
            }

            foreach (var i in t.target.View.inventory.itemList.ToArray ()) {
                i.MoveToContainer (t.source.containerMain);
            }

            TradeCloseBoxes (t);
        }

        void AcceptTrade (BasePlayer player)
        {
            BasePlayer source = null;

            PendingTrade pendingTrade = null;

            foreach (KeyValuePair<BasePlayer, PendingTrade> kvp in pendingTrades) {
                if (kvp.Value.Target == player) {
                    pendingTrade = kvp.Value;
                    source = kvp.Key;
                    break;
                }
            }

            if (source != null && pendingTrade != null) {
                pendingTrade.Destroy ();
                pendingTrades.Remove (source);
                StartTrades (source, player);
            } else {
                SendReply (player, GetMsg ("Status: No Pending", player));
            }
        }

        void StartTrades (BasePlayer source, BasePlayer target)
        {
            var trade = new OpenTrade (onlinePlayers [source], onlinePlayers [target]);
            StartTrade (source, target, trade);
            if (source != target) {
                StartTrade (target, source, trade);
            }
        }

        void StartTrade (BasePlayer source, BasePlayer target, OpenTrade trade)
        {
            OpenBox (source, source);

            if (!openTrades.Contains (trade)) {
                openTrades.Add (trade);
            }
            onlinePlayers [source].Trade = trade;

            timer.In (0.1f, () => ShowTrade (source, trade, GetMsg ("Trade: Pending", source)));
        }

        void OpenBox (BasePlayer player, BaseEntity target)
        {
            SubscribeAll ();
            var ply = onlinePlayers [player];
            if (ply.View == null) {
                OpenBoxView (player, target);
                return;
            }

            CloseBoxView (player, ply.View);
            timer.In (1f, () => OpenBoxView (player, target));
        }

        void OpenBoxView (BasePlayer player, BaseEntity targArg)
        {
            var pos = new Vector3 (player.transform.position.x, player.transform.position.y - 1, player.transform.position.z);
            var boxContainer = GameManager.server.CreateEntity (box, pos) as StorageContainer;
            boxContainer.GetComponent<DestroyOnGroundMissing> ().enabled = false;
            boxContainer.GetComponent<GroundWatch> ().enabled = false;
            boxContainer.transform.position = pos;

            if (!boxContainer) return;

            StorageContainer view = boxContainer as StorageContainer;
            view.limitNetworking = true;
            player.EndLooting ();
            if (targArg is BasePlayer) {

                BasePlayer target = targArg as BasePlayer;
                ItemContainer container = new ItemContainer ();
                container.playerOwner = player;
                container.ServerInitialize (null, slots);
                if (container.uid == 0)
                    container.GiveUID ();

                view.enableSaving = false;
                view.Spawn ();
                view.inventory = container;

                onlinePlayers [player].View = view;
                timer.In (0.1f, () => view.PlayerOpenLoot (player));
            }
        }

        void CloseBoxView (BasePlayer player, StorageContainer view)
        {

            OnlinePlayer onlinePlayer;
            if (!onlinePlayers.TryGetValue (player, out onlinePlayer)) return;
            if (onlinePlayer.View == null) return;

            HideTrade (player);
            if (onlinePlayer.Trade != null) {
                OpenTrade t = onlinePlayer.Trade;
                t.closing = true;

                if (t.sourcePlayer == player && t.targetPlayer != player && t.target.View != null) {
                    t.target.Trade = null;
                    CloseBoxView (t.targetPlayer, t.target.View);
                } else if (t.targetPlayer == player && t.sourcePlayer != player && t.source.View != null) {
                    t.source.Trade = null;
                    CloseBoxView (t.sourcePlayer, t.source.View);
                }

                if (openTrades.Contains (t)) {
                    openTrades.Remove (t);
                }
            }

            if (view.inventory.itemList.Count > 0) {
                foreach (Item item in view.inventory.itemList.ToArray ()) {
                    if (item.position != -1) {
                        item.MoveToContainer (player.inventory.containerMain);
                    }
                }
            }

            if (view.inventory.itemList.Count > 0) {
                foreach (Item item in view.inventory.itemList.ToArray ()) {
                    if (item.position != -1) {
                        item.MoveToContainer (player.inventory.containerBelt);
                    }
                }
            }

            if (player.inventory.loot.entitySource != null) {
                player.inventory.loot.Invoke ("SendUpdate", 0.1f);
                view.SendMessage ("PlayerStoppedLooting", player, SendMessageOptions.DontRequireReceiver);
                player.SendConsoleCommand ("inventory.endloot", null);
            }

            player.inventory.loot.entitySource = null;
            player.inventory.loot.itemSource = null;
            player.inventory.loot.containers = new List<ItemContainer> ();

            view.inventory = new ItemContainer ();

            onlinePlayer.Clear ();

            view.Kill (BaseNetworkable.DestroyMode.None);

            if (onlinePlayers.Values.Count (p => p.View != null) <= 0) {
                UnsubscribeAll ();
            }
        }

        bool CanPlayerTrade (BasePlayer player, string perm)
        {
            if (!permission.UserHasPermission (player.UserIDString, perm)) {
                SendReply (player, GetMsg ("Denied: Permission", player));
                return false;
            }

            if (!player.CanBuild ()) {
                SendReply (player, GetMsg ("Denied: Privilege", player));
                return false;
            }

            if (radiationMax > 0 && player.radiationLevel > radiationMax) {
                SendReply (player, GetMsg ("Denied: Irradiated", player));
                return false;
            }

            if (player.IsSwimming ()) {
                SendReply (player, GetMsg ("Denied: Swimming", player));
                return false;
            }

            if (!player.IsOnGround () || player.IsFlying || player.isInAir) {
                SendReply (player, GetMsg ("Denied: Falling", player));
                return false;
            }

            if (player.isMounted) {
                SendReply (player, GetMsg ("Denied: Mounted", player));
                return false;
            }

            if (player.IsWounded ()) {
                SendReply (player, GetMsg ("Denied: Wounded", player));
                return false;
            }

            if (player.GetComponentInParent<CargoShip> ()) {
                SendReply (player, GetMsg ("Denied: Ship", player));
                return false;
            }

            if (player.GetComponentInParent<HotAirBalloon> ()) {
                SendReply (player, GetMsg ("Denied: Balloon", player));
                return false;
            }

            if (player.GetComponentInParent<Lift> ()) {
                SendReply (player, GetMsg ("Denied: Lift", player));
                return false;
            }

            if (!allowSafeZone && player.InSafeZone ()) {
                SendReply (player, GetMsg ("Denied: Safe Zone", player));
                return false;
            }

            var canTrade = Interface.Call ("CanTrade", player);
            if (canTrade != null) {
                if (canTrade is string) {
                    SendReply (player, Convert.ToString (canTrade));
                } else {
                    SendReply (player, GetMsg ("Denied: Generic", player));
                }
                return false;
            }

            return true;
        }

        #endregion

        #region HelpText
        private void SendHelpText (BasePlayer player)
        {
            var sb = new StringBuilder ()
               .Append ("Trade by <color=#ce422b>http://rustservers.io</color>\n")
               .Append ("  ").Append ("<color=\"#ffd479\">/trade \"Player Name\"</color> - Send trade request").Append ("\n")
               .Append ("  ").Append ("<color=\"#ffd479\">/trade accept</color> - Accept trade request").Append ("\n");
            player.ChatMessage (sb.ToString ());
        }
        #endregion

        #region Helper methods

        private bool IsTradeBox (BaseNetworkable entity)
        {
            foreach (KeyValuePair<BasePlayer, OnlinePlayer> kvp in onlinePlayers) {
                if (kvp.Value.View != null && kvp.Value.View.net != null && entity.net != null && kvp.Value.View.net.ID == entity.net.ID) {
                    return true;
                }
            }

            return false;
        }

        bool hasAccess (BasePlayer player, string permissionname)
        {
            if (player.IsAdmin) return true;
            return permission.UserHasPermission (player.UserIDString, permissionname);
        }

        private BasePlayer FindPlayerByPartialName (string name)
        {
            if (string.IsNullOrEmpty (name))
                return null;
            BasePlayer player = null;
            name = name.ToLower ();
            var awakePlayers = BasePlayer.activePlayerList.ToArray ();
            foreach (var p in awakePlayers) {
                if (p.net == null || p.net.connection == null)
                    continue;

                if (p.displayName == name) {
                    if (player != null)
                        return null;
                    player = p;
                }
            }

            if (player != null)
                return player;
            foreach (var p in awakePlayers) {
                if (p.net == null || p.net.connection == null)
                    continue;

                if (p.displayName.ToLower ().IndexOf (name) >= 0) {
                    if (player != null)
                        return null;
                    player = p;
                }
            }

            return player;
        }

        private T GetConfig<T> (string name, T defaultValue)
        {
            if (Config [name] == null) {
                return defaultValue;
            }

            return (T)Convert.ChangeType (Config [name], typeof (T));
        }

        private T GetConfig<T> (string name, string name2, T defaultValue)
        {
            if (Config [name, name2] == null) {
                return defaultValue;
            }

            return (T)Convert.ChangeType (Config [name, name2], typeof (T));
        }

        string GetMsg (string key, BasePlayer player = null)
        {
            return lang.GetMessage (key, this, player == null ? null : player.UserIDString);
        }

        private string CleanName (string name)
        {
            return JsonConvert.ToString (name.Trim ()).Replace ("\"", "");
        }

        #endregion
    }
}