using System;
using System.Collections.Generic;
using UnityEngine;
using Oxide.Core;
using System.Text;
using System.Linq;

namespace Oxide.Plugins
{
    [Info ("Recycle", "Calytic", "2.1.9")]
    [Description ("Recycle crafted items to base resources")]
    class Recycle : RustPlugin
    {
        #region Configuration

        float cooldownMinutes;
        float refundRatio;
        float scrapMultiplier;
        string box;
        bool npconly;
        List<object> npcids;
        float radiationMax;
        List<object> recyclableTypes;
        bool allowSafeZone;

        #endregion

        #region State

        Dictionary<string, DateTime> recycleCooldowns = new Dictionary<string, DateTime> ();

        class OnlinePlayer
        {
            public BasePlayer Player;
            public BasePlayer Target;
            public StorageContainer View;
            public List<BasePlayer> Matches;

            public OnlinePlayer (BasePlayer player)
            {
            }
        }

        public Dictionary<ItemContainer, ulong> containers = new Dictionary<ItemContainer, ulong> ();

        [OnlinePlayers]
        Hash<BasePlayer, OnlinePlayer> onlinePlayers = new Hash<BasePlayer, OnlinePlayer> ();

        #endregion

        #region Initialization

        protected override void LoadDefaultConfig ()
        {
            Config ["Settings", "box"] = "assets/prefabs/deployable/woodenbox/woodbox_deployed.prefab";
            Config ["Settings", "cooldownMinutes"] = 5;
            Config ["Settings", "refundRatio"] = 0.5f;
            Config ["Settings", "scrapMultiplier"] = 1f;
            Config ["Settings", "radiationMax"] = 1;
            Config ["Settings", "NPCOnly"] = false;
            Config ["Settings", "NPCIDs"] = new List<object> ();
            Config ["Settings", "recyclableTypes"] = GetDefaultRecyclableTypes ();
            Config ["Settings", "allowSafeZone"] = true;
            Config ["VERSION"] = Version.ToString ();
        }

        void Unloaded ()
        {
            foreach (var player in BasePlayer.activePlayerList) {
                OnlinePlayer onlinePlayer;
                if (onlinePlayers.TryGetValue (player, out onlinePlayer) && onlinePlayer.View != null) {
                    CloseBoxView (player, onlinePlayer.View);
                }
            }
        }

        void Init ()
        {
            Unsubscribe (nameof (CanNetworkTo));
            Unsubscribe (nameof (OnEntityTakeDamage));
        }

        void Loaded ()
        {
            permission.RegisterPermission ("recycle.use", this);
            permission.RegisterPermission ("recycle.nocooldown", this);
            LoadMessages ();
            CheckConfig ();

            cooldownMinutes = GetConfig ("Settings", "cooldownMinutes", 5f);

            box = GetConfig ("Settings", "box", "assets/prefabs/deployable/woodenbox/box_wooden.item.prefab");
            refundRatio = GetConfig ("Settings", "refundRatio", 0.5f);
            scrapMultiplier = GetConfig ("Settings", "scrapMultiplier", 1f);
            radiationMax = GetConfig ("Settings", "radiationMax", 1f);
            recyclableTypes = GetConfig ("Settings", "recyclableTypes", GetDefaultRecyclableTypes ());

            npconly = GetConfig ("Settings", "NPCOnly", false);
            npcids = GetConfig ("Settings", "NPCIDs", new List<object> ());
            allowSafeZone = GetConfig ("Settings", "allowSafeZone", true);
        }

        void CheckConfig ()
        {
            if (Config ["VERSION"] == null) {
                // FOR COMPATIBILITY WITH INITIAL VERSIONS WITHOUT VERSIONED CONFIG
                ReloadConfig ();
            } else if (GetConfig ("VERSION", "") != Version.ToString ()) {
                // ADDS NEW, IF ANY, CONFIGURATION OPTIONS
                ReloadConfig ();
            }
        }

        protected void ReloadConfig ()
        {
            Config ["VERSION"] = Version.ToString ();

            // NEW CONFIGURATION OPTIONS HERE
            // END NEW CONFIGURATION OPTIONS

            PrintToConsole ("Upgrading configuration file");
            SaveConfig ();
        }

        void LoadMessages ()
        {
            lang.RegisterMessages (new Dictionary<string, string>
            {
                { "Recycle: Complete", "Recycling <color=lime>{0}</color> to {1}% base materials:" },
                { "Recycle: Item", "    <color=lime>{0}</color> X <color=yellow>{1}</color>" },
                { "Recycle: Invalid", "Cannot recycle that!" },
                { "Denied: Permission", "You lack permission to do that" },
                { "Denied: Privilege", "You lack build privileges and cannot do that" },
                { "Denied: Swimming", "You cannot do that while swimming" },
                { "Denied: Falling", "You cannot do that while falling" },
                { "Denied: Mounted", "You cannot do that while mounted" },
                { "Denied: Wounded", "You cannot do that while wounded" },
                { "Denied: Irradiated", "You cannot do that while irradiated" },
                { "Denied: Generic", "You cannot do that right now" },
                { "Denied: Ship", "You cannot do that while on a ship"},
                { "Denied: Lift", "You cannot do that while on a lift"},
                { "Denied: Balloon", "You cannot do that while on a balloon"},
                { "Denied: Safe Zone", "You cannot do that while in a safe zone"},
                { "Cooldown: Seconds", "You are doing that too often, try again in a {0} seconds(s)." },
                { "Cooldown: Minutes", "You are doing that too often, try again in a {0} minute(s)." },
            }, this);
        }

        List<object> GetDefaultRecyclableTypes ()
        {
            return new List<object> () {
                ItemCategory.Ammunition.ToString(),
                ItemCategory.Attire.ToString(),
                ItemCategory.Common.ToString(),
                ItemCategory.Component.ToString(),
                ItemCategory.Construction.ToString(),
                ItemCategory.Items.ToString(),
                ItemCategory.Medical.ToString(),
                ItemCategory.Misc.ToString(),
                ItemCategory.Tool.ToString(),
                ItemCategory.Traps.ToString(),
                ItemCategory.Weapon.ToString(),
            };
        }

        bool IsRecycleBox (BaseNetworkable entity)
        {
            foreach (KeyValuePair<BasePlayer, OnlinePlayer> kvp in onlinePlayers) {
                if (kvp.Value?.View?.net != null && entity?.net != null && kvp.Value.View.net.ID == entity.net.ID) {
                    return true;
                }
            }

            return false;
        }

        #endregion

        #region Oxide Hooks

        object CanNetworkTo (BaseNetworkable entity, BasePlayer target)
        {
            if (entity == null || target == null || entity == target)
                return null;
            if (target.IsAdmin)
                return null;

            OnlinePlayer onlinePlayer;
            bool IsMyBox = false;
            if (onlinePlayers.TryGetValue (target, out onlinePlayer)) {
                if (onlinePlayer.View != null && onlinePlayer.View.net.ID == entity.net.ID) {
                    IsMyBox = true;
                }
            }

            if (IsRecycleBox (entity) && !IsMyBox)
                return false;

            return null;
        }

        object OnEntityTakeDamage (BaseCombatEntity entity, HitInfo hitInfo)
        {
            if (hitInfo == null)
                return null;

            if (entity == null)
                return null;

            if (IsRecycleBox (entity))
                return false;

            return null;
        }

        void OnPlayerInit (BasePlayer player)
        {
            onlinePlayers [player].View = null;
            onlinePlayers [player].Target = null;
            onlinePlayers [player].Matches = null;
        }

        void OnPlayerDisconnected (BasePlayer player)
        {
            if (onlinePlayers [player].View != null) {
                CloseBoxView (player, onlinePlayers [player].View);
            }
        }

        void OnPlayerLootEnd (PlayerLoot inventory)
        {
            BasePlayer player;
            if ((player = inventory.GetComponent<BasePlayer> ()) == null)
                return;

            OnlinePlayer onlinePlayer;
            if (onlinePlayers.TryGetValue (player, out onlinePlayer) && onlinePlayer.View != null) {
                if (onlinePlayer.View == inventory.entitySource) {
                    CloseBoxView (player, (StorageContainer)inventory.entitySource);
                }
            }
        }

        void OnItemAddedToContainer (ItemContainer container, Item item)
        {
            if (container.playerOwner is BasePlayer) {
                if (onlinePlayers.ContainsKey (container.playerOwner)) {
                    BasePlayer owner = container.playerOwner;
                    if (containers.ContainsKey (container)) {
                        if (SalvageItem (owner, item)) {
                            item.Remove (0f);
                            item.RemoveFromContainer ();
                        } else {
                            ShowNotification (owner, GetMsg ("Recycle: Invalid", owner));
                            if (!owner.inventory.containerMain.IsFull ()) {
                                item.MoveToContainer (owner.inventory.containerMain);
                            } else if (!owner.inventory.containerBelt.IsFull ()) {
                                item.MoveToContainer (owner.inventory.containerBelt);
                            }
                        }
                    }
                }
            }
        }

        void OnUseNPC (BasePlayer npc, BasePlayer player)
        {
            if (!npcids.Contains (npc.UserIDString))
                return;
            ShowBox (player, player);
        }

        void AddNpc (string id)
        {
            npcids.Add (id);
        }

        void RemoveNpc (string id)
        {
            if (npcids.Contains (id)) {
                npcids.Remove (id);
            }
        }

        #endregion

        #region Commands

        [ConsoleCommand ("rec")]
        void ccRec (ConsoleSystem.Arg arg)
        {
            cmdRec (arg.Connection.player as BasePlayer, arg.cmd.Name, arg.Args);
        }

        //[ConsoleCommand ("receverything")]
        //void ccRecEverything (ConsoleSystem.Arg arg)
        //{
        //    if (arg.connection == null) return;
        //    if (arg.connection.authLevel < 1) return;
        //    List<ItemDefinition> items = ItemManager.GetItemDefinitions ();

        //    foreach (ItemDefinition def in items) {
        //        SalvageItem (arg.Connection.player as BasePlayer, ItemManager.Create (def, 1));
        //    }

        //}


        [ChatCommand ("rec")]
        void cmdRec (BasePlayer player, string command, string [] args)
        {
            if (npconly)
                return;

            ShowBox (player, player);
        }

        #endregion

        #region Core Methods

        void ShowBox (BasePlayer player, BaseEntity target)
        {
            string playerID = player.userID.ToString ();

            if (!CanPlayerRecycle (player))
                return;

            if (cooldownMinutes > 0 && !player.IsAdmin && !permission.UserHasPermission (player.UserIDString, "recycle.nocooldown")) {
                DateTime startTime;

                if (recycleCooldowns.TryGetValue (playerID, out startTime)) {
                    DateTime endTime = DateTime.Now;

                    TimeSpan span = endTime.Subtract (startTime);
                    if (span.TotalMinutes > 0 && span.TotalMinutes < Convert.ToDouble (cooldownMinutes)) {
                        double timeleft = System.Math.Round (Convert.ToDouble (cooldownMinutes) - span.TotalMinutes, 2);
                        if (span.TotalSeconds < 0) {
                            recycleCooldowns.Remove (playerID);
                        }

                        if (timeleft < 1) {
                            double timelefts = System.Math.Round ((Convert.ToDouble (cooldownMinutes) * 60) - span.TotalSeconds);
                            SendReply (player, string.Format (GetMsg ("Cooldown: Seconds", player), timelefts.ToString ()));
                            return;
                        }

                        SendReply (player, string.Format (GetMsg ("Cooldown: Minutes", player), System.Math.Round (timeleft).ToString ()));
                        return;

                    }

                    recycleCooldowns.Remove (playerID);
                }
            }

            if (!recycleCooldowns.ContainsKey (player.userID.ToString ())) {
                recycleCooldowns.Add (player.userID.ToString (), DateTime.Now);
            }
            var ply = onlinePlayers [player];
            if (ply.View == null) {
                if (!OpenBoxView (player, target)) {
                    recycleCooldowns.Remove (playerID);
                }
                return;
            }

            CloseBoxView (player, ply.View);

            NextFrame (delegate () {
                if (!OpenBoxView (player, target)) {
                    recycleCooldowns.Remove (playerID);
                }
            });
        }

        void HideBox (BasePlayer player)
        {
            player.EndLooting ();
            var ply = onlinePlayers [player];
            if (ply.View == null) {
                return;
            }

            CloseBoxView (player, ply.View);
        }

        bool OpenBoxView (BasePlayer player, BaseEntity targArg)
        {
            Subscribe (nameof (CanNetworkTo));
            Subscribe (nameof (OnEntityTakeDamage));

            var pos = new Vector3 (player.transform.position.x, player.transform.position.y - 0.6f, player.transform.position.z);
            int slots = 1;
            var view = GameManager.server.CreateEntity (box, pos) as StorageContainer;

            if (!view)
                return false;

            view.GetComponent<DestroyOnGroundMissing> ().enabled = false;
            view.GetComponent<GroundWatch> ().enabled = false;
            view.transform.position = pos;

            player.EndLooting ();
            if (targArg is BasePlayer) {
                BasePlayer target = targArg as BasePlayer;
                ItemContainer container = new ItemContainer ();
                container.playerOwner = player;
                container.ServerInitialize ((Item)null, slots);
                if ((int)container.uid == 0)
                    container.GiveUID ();

                if (!containers.ContainsKey (container)) {
                    containers.Add (container, player.userID);
                }


                view.enableSaving = false;
                view.Spawn ();
                view.inventory = container;
                view.SendNetworkUpdate (BasePlayer.NetworkQueue.Update);
                onlinePlayers [player].View = view;
                onlinePlayers [player].Target = target;
                timer.Once (0.2f, delegate () {
                    if (onlinePlayers [player].View != null) {
                        onlinePlayers [player].View.PlayerOpenLoot (player);
                    }
                });

                return true;
            }

            return false;
        }

        void CloseBoxView (BasePlayer player, StorageContainer view)
        {
            OnlinePlayer onlinePlayer;
            if (!onlinePlayers.TryGetValue (player, out onlinePlayer))
                return;
            if (onlinePlayer.View == null)
                return;

            if (containers.ContainsKey (view.inventory)) {
                containers.Remove (view.inventory);
            }

            player.inventory.loot.containers = new List<ItemContainer> ();
            view.inventory = new ItemContainer ();

            if (player.inventory.loot.IsLooting ()) {
                player.SendConsoleCommand ("inventory.endloot", null);
            }

            onlinePlayer.View = null;
            onlinePlayer.Target = null;

            NextFrame (delegate () {
                view.KillMessage ();

                if (onlinePlayers.Values.Count (p => p.View != null) <= 0) {
                    Unsubscribe (nameof (CanNetworkTo));
                    Unsubscribe (nameof (OnEntityTakeDamage));
                }
            });
        }

        //        bool SalvageItem(BasePlayer player, Item item)
        //        {
        //            var sb = new StringBuilder();
        //
        //            var ratio = item.hasCondition ? (item.condition / item.maxCondition) : 1;
        //
        //            sb.Append(string.Format(GetMsg("Recycle: Complete", player), item.info.displayName.english, (refundRatio * 100)));
        //
        //            if (item.info.Blueprint == null)
        //            {
        //                return false;
        //            }
        //
        //            foreach (var ingredient in item.info.Blueprint.ingredients)
        //            {
        //                if (!ingredient.itemDef.shortname == "scrap")
        //                {
        //                    var refundAmount = (double)ingredient.amount / item.info.Blueprint.amountToCreate;
        //                    refundAmount *= item.amount;
        //                    refundAmount *= ratio;
        //                    refundAmount *= refundRatio;
        //                    refundAmount = System.Math.Ceiling(refundAmount);
        //                    if (refundAmount < 1)
        //                        refundAmount = 1;
        //
        //                    var newItem = ItemManager.Create(ingredient.itemDef, (int)refundAmount);
        //
        //                    ItemBlueprint ingredientBp = ingredient.itemDef.Blueprint;
        //                    if (item.hasCondition)
        //                        newItem.condition = (float)System.Math.Ceiling(newItem.maxCondition * ratio);
        //
        //                    player.GiveItem(newItem);
        //                            sb.AppendLine();
        //                            sb.Append(string.Format(GetMsg("Recycle: Item", player), newItem.info.displayName.english, newItem.amount));
        //                }
        //            }
        //
        //            ShowNotification(player, sb.ToString());
        //
        //            return true;
        //        }
        //

        List<string> exclude = new List<string> () {
            "scrap",
            "antiradpills",
            "battery.small",
            "blood",
            "blueprintbase"
        };

        bool SalvageItem (BasePlayer player, Item item)
        {
            if (item == null) {
                return false;
            }

            if (!recyclableTypes.Contains (Enum.GetName (typeof (ItemCategory), item.info.category))) {
                return false;
            }

            if (item.info.category == ItemCategory.Food) {
                return false;
            }

            if (exclude.Contains (item.info.shortname)) {
                return false;
            }

            var refundAmount = refundRatio;
            var sb = new StringBuilder ();

            if (item.hasCondition) {
                refundAmount = Mathf.Clamp01 (refundAmount * Mathf.Clamp (item.conditionNormalized * item.maxConditionNormalized, 0.1f, 1f));
            }

            var amountToConsume = 1;
            if (item.amount > 1) {
                amountToConsume = Mathf.CeilToInt (Mathf.Min (item.amount, item.info.stackable * 0.1f));
            }

            if (item.info.Blueprint != null && item.info.Blueprint.scrapFromRecycle > 0) {
                float scrapAmount = (item.info.Blueprint.scrapFromRecycle * scrapMultiplier) * amountToConsume;
                var newItem = ItemManager.CreateByName ("scrap", Convert.ToInt32 (scrapAmount));
                if (newItem != null) {
                    player.GiveItem (newItem);
                    sb.AppendLine ();
                    sb.Append (string.Format (GetMsg ("Recycle: Item", player), newItem.info.displayName.english, newItem.amount));
                }
            }


            if (item.info.Blueprint != null && item.info.Blueprint.ingredients.Count > 0) {
                item.UseItem (amountToConsume);
                foreach (ItemAmount current in item.info.Blueprint.ingredients.OrderBy (x => Core.Random.Range (0, 1000))) {
                    if (!(current.itemDef.shortname == "scrap") && item.info.Blueprint != null) {
                        var refundMultiplier = current.amount / item.info.Blueprint.amountToCreate;
                        var refundMaximum = 0;
                        if (refundMultiplier <= 1) {
                            for (var index = 0; index < amountToConsume; ++index) {
                                if (Core.Random.Range (0, 1) <= refundAmount) {
                                    ++refundMaximum;
                                }
                            }
                        } else {
                            refundMaximum = Mathf.CeilToInt (Mathf.Clamp (refundMultiplier * refundAmount, 0f, current.amount) * amountToConsume);
                        }

                        if (refundMaximum > 0) {
                            int refundIterations = Mathf.Clamp (Mathf.CeilToInt (refundMaximum / current.itemDef.stackable), 1, refundMaximum);
                            for (var index = 0; index < refundIterations; ++index) {
                                var itemAmount = refundMaximum <= current.itemDef.stackable ? refundMaximum : current.itemDef.stackable;
                                var newItem = ItemManager.Create (current.itemDef, itemAmount);
                                if (newItem != null) {
                                    player.GiveItem (newItem);
                                    sb.AppendLine ();
                                    sb.Append (string.Format (GetMsg ("Recycle: Item", player), newItem.info.displayName.english, newItem.amount));

                                    refundMaximum -= itemAmount;
                                    if (refundMaximum <= 0)
                                        break;
                                }
                            }
                        }
                    }
                }
            }

            var msg = sb.ToString ();
            if (msg != string.Empty) {
                ShowNotification (player, msg);
                if (Interface.Oxide.CallHook ("OnRecycleItemSalvaged", player, item) != null) {
                    return false;
                }

                return true;
            }

            return false;
        }

        bool CanPlayerRecycle (BasePlayer player)
        {
            if (!permission.UserHasPermission (player.UserIDString, "recycle.use")) {
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

            var canRecycle = Interface.Call ("CanRecycleCommand", player);
            if (canRecycle != null) {
                if (canRecycle is string) {
                    SendReply (player, Convert.ToString (canRecycle));
                } else {
                    SendReply (player, GetMsg ("Denied: Generic", player));
                }
                return false;
            }

            return true;
        }

        #endregion

        #region GUI

        public string jsonNotify = @"[{""name"":""NotifyMsg"",""parent"":""Overlay"",""components"":[{""type"":""UnityEngine.UI.Image"",""color"":""0 0 0 0.89""},{""type"":""RectTransform"",""anchormax"":""0.99 0.94"",""anchormin"":""0.69 0.77""}]},{""name"":""MassText"",""parent"":""NotifyMsg"",""components"":[{""type"":""UnityEngine.UI.Text"",""text"":""{msg}"",""fontSize"":16,""align"":""UpperLeft""},{""type"":""RectTransform"",""anchormax"":""0.98 0.99"",""anchormin"":""0.01 0.02""}]},{""name"":""CloseButton{1}"",""parent"":""NotifyMsg"",""components"":[{""type"":""UnityEngine.UI.Button"",""color"":""0.95 0 0 0.68"",""close"":""NotifyMsg"",""imagetype"":""Tiled""},{""type"":""RectTransform"",""anchormax"":""0.99 1"",""anchormin"":""0.91 0.86""}]},{""name"":""CloseButtonLabel"",""parent"":""CloseButton{1}"",""components"":[{""type"":""UnityEngine.UI.Text"",""text"":""X"",""fontSize"":10,""align"":""MiddleCenter""},{""type"":""RectTransform"",""anchormax"":""1 1"",""anchormin"":""0 0""}]}]";

        public void ShowNotification (BasePlayer player, string msg)
        {
            HideNotification (player);
            string send = jsonNotify.Replace ("{msg}", msg);

            CommunityEntity.ServerInstance.ClientRPCEx (new Network.SendInfo { connection = player.net.connection }, null, "AddUI", send);
            timer.Once (3f, delegate () {
                HideNotification (player);
            });
        }

        public void HideNotification (BasePlayer player)
        {
            CommunityEntity.ServerInstance.ClientRPCEx (new Network.SendInfo { connection = player.net.connection }, null, "DestroyUI", "NotifyMsg");
        }

        #endregion

        #region HelpText

        void SendHelpText (BasePlayer player)
        {
            var sb = new StringBuilder ()
               .Append ("Recycle by <color=#ce422b>http://rustservers.io</color>\n")
               .Append ("  ").Append ("<color=\"#ffd479\">/rec</color> - Open recycle box").Append ("\n");
            player.ChatMessage (sb.ToString ());
        }

        #endregion

        #region Helper methods

        string GetMsg (string key, BasePlayer player = null)
        {
            return lang.GetMessage (key, this, player == null ? null : player.UserIDString);
        }

        T GetConfig<T> (string name, T defaultValue)
        {
            if (Config [name] == null) {
                Config [name] = defaultValue;
                Config.Save ();
                return defaultValue;
            }

            return (T)Convert.ChangeType (Config [name], typeof (T));
        }

        T GetConfig<T> (string name, string name2, T defaultValue)
        {
            if (Config [name, name2] == null) {
                Config [name, name2] = defaultValue;
                Config.Save ();
                return defaultValue;
            }

            return (T)Convert.ChangeType (Config [name, name2], typeof (T));
        }

        #endregion
    }
}