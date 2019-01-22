using System.Collections.Generic;
using System;
using Oxide.Core;
using Oxide.Core.Plugins;
using System.Linq;
using Oxide.Core.Configuration;
using UnityEngine;
using Newtonsoft.Json;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("Bounty", "k1lly0u", "0.2.03")]
    class Bounty : RustPlugin
    {
        [PluginReference] Plugin Clans, Friends, PopupNotifications, Economics, ServerRewards;

        private StoredData storedData;
        private DynamicConfigFile data;

        private Dictionary<ulong, ulong> bountyCreator = new Dictionary<ulong, ulong>();
        private Dictionary<StorageContainer, ulong> openContainers = new Dictionary<StorageContainer, ulong>();
        private Dictionary<int, string> idToDisplayName = new Dictionary<int, string>();

        private string boxPrefab = "assets/prefabs/deployable/woodenbox/woodbox_deployed.prefab";

        #region Oxide Hooks     
        private void Loaded()
        {
            permission.RegisterPermission("bounty.use", this);
            permission.RegisterPermission("bounty.admin", this);

            lang.RegisterMessages(messages, this);

            data = Interface.Oxide.DataFileSystem.GetFile("Bounty/bounty_data");
        }

        private void OnServerInitialized()
        {
            idToDisplayName = ItemManager.itemList.ToDictionary(x => x.itemid, y => y.displayName.english);
            LoadData();

            foreach (BasePlayer player in BasePlayer.activePlayerList)
                OnPlayerInit(player);
        }
        
        private void OnPlayerInit(BasePlayer player)
        {
            PlayerData playerData;
            if (storedData.players.TryGetValue(player.userID, out playerData))
            {
                if (playerData.activeBounties.Count > 0)                
                    BroadcastToPlayer(player, string.Format(msg("bounties_outstanding", player.userID), playerData.activeBounties.Count));
                
                playerData.displayName = player.displayName;
            }            
        }

        private void OnPlayerDisconnected(BasePlayer player)
        {
            PlayerData playerData;
            if (storedData.players.TryGetValue(player.userID, out playerData))
                playerData.UpdateWantedTime();
        }

        private object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null || info == null)
                return null;

            if (entity is StorageContainer && openContainers.ContainsKey(entity as StorageContainer))
                return false;

            return null;
        }

        private void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null || info == null)
                return;

            BasePlayer victim = entity.ToPlayer();
            BasePlayer attacker = info.InitiatorPlayer;

            if (victim == null || attacker == null || attacker.GetComponent<NPCPlayer>())
                return;
            
            PlayerData victimData;
            if (!storedData.players.TryGetValue(victim.userID, out victimData))
                return;

            if (victimData.activeBounties.Count == 0)
                return;

            if (IsFriendlyPlayer(victim.userID, attacker.userID))
            {
                BroadcastToPlayer(attacker, msg("is_friend", attacker.userID));
                return;
            }
           
            victimData.UpdateWantedTime();

            List<int> rewards = victimData.activeBounties.Select(x => x.rewardId).ToList();
            victimData.activeBounties.Clear();

            PlayerData attackerData;
            if (!storedData.players.TryGetValue(attacker.userID, out attackerData))
            {
                attackerData = new PlayerData(attacker.displayName);
                storedData.players.Add(attacker.userID, attackerData);
            }

            attackerData.ClaimRewards(rewards);
            BroadcastToPlayer(attacker, string.Format(msg("rewards_pending"), victim.displayName, rewards.Count));            
        }

        private object CanLootEntity(BasePlayer player, StorageContainer container)
        {
            if (player == null || container == null || !openContainers.ContainsKey(container))
                return null;

            if (openContainers[container] != player.userID)
                return false;
            
            return null;
        }

        private void OnPlayerLootEnd(PlayerLoot inventory)
        {
            BasePlayer player = inventory.GetComponent<BasePlayer>();

            if (bountyCreator.ContainsKey(player.userID))
            {
                StorageContainer container = inventory.entitySource.GetComponent<StorageContainer>();
                if (container != null)
                {
                    if (container.inventory.itemList.Count == 0)
                        SendReply(player, msg("no_items_deposited", player.userID));
                    else CreateNewBounty(player, bountyCreator[player.userID], 0, 0, container.inventory);

                    openContainers.Remove(container);
                    ClearContainer(container.inventory);
                    container.DieInstantly();
                }
                bountyCreator.Remove(player.userID);
            }
        }

        private void OnServerSave() => SaveData();
       
        private void Unload() => SaveData(); 
        #endregion

        #region Functions  
        private void BroadcastToPlayer(BasePlayer player, string message)
        {
            if (configData.Notifications.UsePopupNotifications && PopupNotifications)
                PopupNotifications?.Call("CreatePopupOnPlayer", message, player, configData.Notifications.PopupDuration);
            else SendReply(player, message);
        }

        private void CreateNewBounty(BasePlayer initiator, ulong targetId, int rpAmount, int ecoAmount, ItemContainer container)
        {
            IPlayer target = covalence.Players.FindPlayerById(targetId.ToString());
            
            PlayerData playerData;
            if (!storedData.players.TryGetValue(targetId, out playerData))
            {
                playerData = new PlayerData(target?.Name ?? "No Name");
                storedData.players.Add(targetId, playerData);
            }

            playerData.totalBounties++;

            int rewardId = GetUniqueId();
            storedData.rewards.Add(rewardId, new RewardInfo(rpAmount, ecoAmount, container));
            playerData.activeBounties.Add(new PlayerData.BountyInfo(initiator.userID, initiator.displayName, rewardId));

            BasePlayer targetPlayer = target?.Object as BasePlayer;
            if (targetPlayer != null)
                BroadcastToPlayer(targetPlayer, string.Format(msg("bounty_placed_target", targetPlayer.userID), initiator.displayName));

            BroadcastToPlayer(initiator, string.Format(msg("bounty_placed_initiator", initiator.userID), target?.Name ?? "No Name"));
        }

        private void CancelBounty(BasePlayer player, IPlayer target)
        {
            PlayerData playerData;
            if (!storedData.players.TryGetValue(ulong.Parse(target.Id), out playerData))
            {
                SendReply(player, string.Format(msg("no_bounty_placed", player.userID), target.Name));
                return;
            }

            PlayerData.BountyInfo bountyInfo = playerData.activeBounties.Find(x => x.initiatorId == player.userID) ?? null;
            if (bountyInfo == null)
            {
                SendReply(player, string.Format(msg("no_bounty_placed", player.userID), target.Name));
                return;
            }

            RewardInfo rewardInfo = storedData.rewards[bountyInfo.rewardId];
            GivePlayerRewards(player, rewardInfo);
            storedData.rewards.Remove(bountyInfo.rewardId);
            playerData.activeBounties.Remove(bountyInfo);

            BasePlayer targetPlayer = target.Object as BasePlayer;
            if (targetPlayer != null)            
                BroadcastToPlayer(targetPlayer, string.Format(msg("bounty_cancelled_target", targetPlayer.userID), player.displayName));

            BroadcastToPlayer(player, string.Format(msg("bounty_cancelled_initiator", player.userID), target.Name));
        }

        private void GivePlayerRewards(BasePlayer player, RewardInfo rewardInfo)
        {
            if (rewardInfo.econAmount > 0 && Economics)            
                Economics?.Call("Deposit", player.UserIDString, (double)rewardInfo.econAmount);
            
            if (rewardInfo.rpAmount > 0 && ServerRewards)
                ServerRewards?.Call("AddPoints", player.userID, rewardInfo.rpAmount);

            if (rewardInfo.rewardItems.Count > 0)
            {
                foreach (RewardInfo.ItemData itemData in rewardInfo.rewardItems)
                {
                    Item item = CreateItem(itemData);
                    player.GiveItem(item, BaseEntity.GiveItemReason.PickedUp);
                }
            }

            SendReply(player, msg("reward_claimed", player.userID));
        }

        private Item CreateItem(RewardInfo.ItemData itemData)
        {
            Item item = ItemManager.CreateByItemID(itemData.itemid, itemData.amount, itemData.skin);
            item.condition = itemData.condition;

            if (itemData.instanceData != null)
                itemData.instanceData.Restore(item);

            BaseProjectile weapon = item.GetHeldEntity() as BaseProjectile;
            if (weapon != null)
            {
                if (!string.IsNullOrEmpty(itemData.ammotype))
                    weapon.primaryMagazine.ammoType = ItemManager.FindItemDefinition(itemData.ammotype);
                weapon.primaryMagazine.contents = itemData.ammo;
            }
            if (itemData.contents != null)
            {
                foreach (var contentData in itemData.contents)
                {
                    var newContent = ItemManager.CreateByItemID(contentData.itemid, contentData.amount);
                    if (newContent != null)
                    {
                        newContent.condition = contentData.condition;
                        newContent.MoveToContainer(item.contents);
                    }
                }
            }
            return item;
        }

        private void SpawnItemContainer(BasePlayer player)
        {
            StorageContainer container = (StorageContainer)GameManager.server.CreateEntity(boxPrefab, player.transform.position + player.eyes.BodyForward(), new Quaternion(), true);
            container.enableSaving = false;
            container.Spawn();
            
            openContainers.Add(container, player.userID);
            timer.In(0.15f, ()=> OpenInventory(player, container));            
        }
        
        private void OpenInventory(BasePlayer player, StorageContainer container)
        {
            player.inventory.loot.Clear();
            player.inventory.loot.entitySource = container;
            player.inventory.loot.itemSource = null;
            player.inventory.loot.AddContainer(container.inventory);
            player.inventory.loot.SendImmediate();
            player.ClientRPCPlayer(null, player, "RPC_OpenLootPanel", "generic");
            player.SendNetworkUpdate();
        }

        private void ClearContainer(ItemContainer itemContainer)
        {
            if (itemContainer == null || itemContainer.itemList == null) return;
            while (itemContainer.itemList.Count > 0)
            {
                var item = itemContainer.itemList[0];
                item.RemoveFromContainer();
                item.Remove(0f);
            }
        }

        private int GetUniqueId()
        {
            int uid = UnityEngine.Random.Range(0, 10000);
            if (storedData.rewards.ContainsKey(uid))
                return GetUniqueId();
            return uid;
        }
        
        private double CurrentTime() => DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1, 0, 0, 0)).TotalSeconds;

        private List<BasePlayer> FindPlayer(string partialNameOrId)
        {
            List<BasePlayer> players = new List<BasePlayer>();
            foreach(BasePlayer player in BasePlayer.activePlayerList)
            {
                if (partialNameOrId == player.UserIDString)
                    return new List<BasePlayer>() { player };

                if (player.displayName.ToLower().Contains(partialNameOrId.ToLower()))
                    players.Add(player);
            }
            return players;
        }

        private string FormatTime(double time)
        {
            TimeSpan dateDifference = TimeSpan.FromSeconds((float)time);
            var days = dateDifference.Days;
            var hours = dateDifference.Hours;
            hours += (days * 24);
            var mins = dateDifference.Minutes;
            var secs = dateDifference.Seconds;
            if (hours > 0)
                return string.Format("{0:00}h {1:00}m {2:00}s", hours, mins, secs);
            else return string.Format("{0:00}m {1:00}s", mins, secs);
        }
        #endregion

        #region Friends
        public bool IsFriendlyPlayer(ulong playerId, ulong friendId)
        {
            if (playerId == friendId || IsFriend(playerId, friendId) || IsClanmate(playerId, friendId))
                return true;
            return false;
        }

        private bool IsClanmate(ulong playerId, ulong friendId)
        {
            if (!Clans || !configData.IgnoreClans) return false;
            object playerTag = Clans?.Call("GetClanOf", playerId);
            object friendTag = Clans?.Call("GetClanOf", friendId);
            if ((playerTag is string && !string.IsNullOrEmpty((string)playerTag)) && (friendTag is string && !string.IsNullOrEmpty((string)friendTag)))
                if (playerTag == friendTag) return true;
            return false;
        }

        private bool IsFriend(ulong playerID, ulong friendID)
        {
            if (!Friends || !configData.IgnoreFriends) return false;
            return (bool)Friends?.Call("AreFriends", playerID, friendID);
        }
        #endregion

        #region Commands       
        [ChatCommand("bounty")]
        private void cmdBounty(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, "bounty.use"))
            {
                SendReply(player, msg("no_permission", player.userID));
                return;
            }

            if (args.Length == 0)
            {
                SendReply(player, string.Format(msg("title", player.userID), Title, Version));
                if (configData.Rewards.AllowItems)
                    SendReply(player, msg("help1", player.userID));
                if (configData.Rewards.AllowServerRewards && ServerRewards)
                    SendReply(player, msg("help2", player.userID));
                if (configData.Rewards.AllowEconomics && Economics)
                    SendReply(player, msg("help3", player.userID));
                SendReply(player, msg("help4", player.userID));
                SendReply(player, msg("help5", player.userID));
                SendReply(player, msg("help6", player.userID));
                SendReply(player, msg("help7", player.userID));
                SendReply(player, msg("help8", player.userID));

                if (player.IsAdmin || permission.UserHasPermission(player.UserIDString, "bounty.admin"))                
                    SendReply(player, msg("help9", player.userID));

                return;
            }

            switch (args[0].ToLower())
            {
                case "add":
                    {                        
                        if (args.Length < 3)
                        {
                            SendReply(player, msg("invalid_syntax", player.userID));
                            return;
                        }

                        List<BasePlayer> players = FindPlayer(args[2]);
                        if (players.Count == 0)
                        {
                            SendReply(player, msg("no_player_found", player.userID));
                            return;
                        }
                        if (players.Count > 1)
                        {
                            SendReply(player, msg("multiple_players_found", player.userID));
                            return;
                        }

                        BasePlayer targetPlayer = players[0];
                        if (targetPlayer == null)
                        {
                            SendReply(player, msg("no_player_found", player.userID));
                            return;
                        }

                        if (targetPlayer == player)
                        {
                            SendReply(player, msg("cant_bounty_self", player.userID));
                            return;
                        }

                        PlayerData playerData;
                        if (!storedData.players.TryGetValue(targetPlayer.userID, out playerData))
                        {
                            playerData = new PlayerData(targetPlayer.displayName);

                            storedData.players.Add(targetPlayer.userID, playerData);
                        }

                        if (playerData.activeBounties.Find(x => x.initiatorId == player.userID) != null)
                        {
                            SendReply(player, msg("has_active_bounty", player.userID));
                            return;
                        }

                        switch (args[1].ToLower())
                        {
                            case "items":
                                SpawnItemContainer(player);

                                if (bountyCreator.ContainsKey(player.userID))
                                    bountyCreator[player.userID] = targetPlayer.userID;
                                else bountyCreator.Add(player.userID, targetPlayer.userID);
                                return;
                            case "rp":
                                if (!configData.Rewards.AllowServerRewards || !ServerRewards || args.Length != 4)
                                {
                                    SendReply(player, msg("invalid_syntax", player.userID));
                                    return;
                                }

                                int rpAmount;
                                if (!int.TryParse(args[3], out rpAmount))
                                {
                                    SendReply(player, msg("no_value_entered", player.userID));
                                    return;
                                }

                                int availableRp = (int)ServerRewards?.Call("CheckPoints", player.userID);

                                if (availableRp < rpAmount || !(bool)ServerRewards?.Call("TakePoints", player.userID, rpAmount))
                                {
                                    SendReply(player, msg("not_enough_rp", player.userID));
                                    return;
                                }

                                CreateNewBounty(player, targetPlayer.userID, rpAmount, 0, null);
                                return;
                            case "eco":
                                if (!configData.Rewards.AllowEconomics || !Economics || args.Length != 4)
                                {
                                    SendReply(player, msg("invalid_syntax", player.userID));
                                    return;
                                }

                                int ecoAmount;
                                if (!int.TryParse(args[3], out ecoAmount))
                                {
                                    SendReply(player, msg("no_value_entered", player.userID));
                                    return;
                                }

                                double availableEco = (double)Economics?.Call("Balance", player.UserIDString);

                                if (availableEco < ecoAmount || !(bool)Economics?.Call("Withdraw", player.UserIDString, (double)ecoAmount))
                                {
                                    SendReply(player, msg("not_enough_eco", player.userID));
                                    return;
                                }

                                CreateNewBounty(player, targetPlayer.userID, 0, ecoAmount, null);
                                return;

                            default:
                                SendReply(player, msg("invalid_syntax", player.userID));
                                return;
                        }
                    }
                case "cancel":
                    {
                        if (args.Length < 2)
                        {
                            SendReply(player, msg("invalid_syntax", player.userID));
                            return;
                        }

                        IPlayer targetPlayer = covalence.Players.FindPlayer(args[1]);
                        if (targetPlayer == null)
                        {
                            SendReply(player, msg("no_player_found", player.userID));
                            return;
                        }

                        CancelBounty(player, targetPlayer);
                    }
                    return;
                case "claim":
                    {
                        PlayerData playerData;
                        if (!storedData.players.TryGetValue(player.userID, out playerData) || playerData.unclaimedRewards.Count == 0)
                        {
                            SendReply(player, msg("no_rewards_pending", player.userID));
                            return;
                        }

                        if (args.Length < 2)
                        {
                            SendReply(player, msg("help10", player.userID));
                            foreach(int rewardId in playerData.unclaimedRewards)
                            {
                                RewardInfo rewardInfo = storedData.rewards[rewardId];
                                string reward = string.Empty;
                                if (rewardInfo.rewardItems.Count > 1)
                                {
                                    for (int i = 0; i < rewardInfo.rewardItems.Count; i++)
                                    {
                                        RewardInfo.ItemData itemData = rewardInfo.rewardItems.ElementAt(i);
                                        reward += (string.Format(msg("reward_item", player.userID), itemData.amount, idToDisplayName[itemData.itemid]) + (i < rewardInfo.rewardItems.Count - 1 ? ", " : ""));
                                    }
                                }
                                else reward = rewardInfo.econAmount > 0 ? string.Format(msg("reward_econ", player.userID), rewardInfo.econAmount) : string.Format(msg("reward_rp", player.userID), rewardInfo.rpAmount);

                                SendReply(player, string.Format(msg("reward_info", player.userID), rewardId, reward));
                            }
                        }
                        else
                        {
                            int rewardId;
                            if (!int.TryParse(args[1], out rewardId) || !playerData.unclaimedRewards.Contains(rewardId))
                            {
                                SendReply(player, msg("invalid_reward_id", player.userID));
                                return;
                            }

                            RewardInfo rewardInfo = storedData.rewards[rewardId];
                            GivePlayerRewards(player, rewardInfo);
                            storedData.rewards.Remove(rewardId);
                            playerData.unclaimedRewards.Remove(rewardId);
                        }
                    }
                    return;
                case "view":
                    {
                        if (args.Length < 2)
                        {
                            SendReply(player, msg("invalid_syntax", player.userID));
                            return;
                        }

                        IPlayer targetPlayer = covalence.Players.FindPlayer(args[1]);
                        if (targetPlayer == null)
                        {
                            SendReply(player, msg("no_player_found", player.userID));
                            return;
                        }

                        PlayerData playerData;
                        if (!storedData.players.TryGetValue(ulong.Parse(targetPlayer.Id), out playerData) || playerData.activeBounties.Count == 0)
                        {
                            SendReply(player, msg("no_active_bounties", player.userID));
                            return;
                        }

                        SendReply(player, string.Format(msg("player_has_bounties", player.userID), targetPlayer.Name, playerData.activeBounties.Count));
                        foreach(var bounty in playerData.activeBounties)
                        {
                            RewardInfo rewardInfo = storedData.rewards[bounty.rewardId];
                            string reward = string.Empty;
                            if (rewardInfo.rewardItems.Count > 0)
                            {
                                for (int i = 0; i < rewardInfo.rewardItems.Count; i++)
                                {
                                    RewardInfo.ItemData itemData = rewardInfo.rewardItems.ElementAt(i);
                                    reward += (string.Format(msg("reward_item", player.userID), itemData.amount, idToDisplayName[itemData.itemid]) + (i < rewardInfo.rewardItems.Count - 1 ? ", " : ""));
                                }
                            }
                            else reward = rewardInfo.econAmount > 0 ? string.Format(msg("reward_econ", player.userID), rewardInfo.econAmount) : string.Format(msg("reward_rp", player.userID), rewardInfo.rpAmount);

                            SendReply(player, string.Format(msg("bounty_info", player.userID), bounty.initiatorName, FormatTime(CurrentTime() - bounty.initiatedTime), reward));
                        }
                    }
                    return;
                case "top":
                    IEnumerable<PlayerData> top10Hunters = storedData.players.Values.OrderByDescending(x => x.bountiesClaimed).Take(10);
                    string hunterMessage = msg("top_hunters", player.userID);

                    for (int i = 0; i < top10Hunters.Count(); i++)
                    {
                        PlayerData playerData = top10Hunters.ElementAt(i);
                        hunterMessage += string.Format(msg("top_hunter_entry", player.userID), playerData.displayName, playerData.bountiesClaimed);

                        if (i == 4)
                        {
                            SendReply(player, hunterMessage);
                            hunterMessage = string.Empty;
                        }
                    }
                    //foreach (PlayerData playerData in top10Hunters)
                       // hunterMessage += string.Format(msg("top_hunter_entry", player.userID), playerData.displayName, playerData.bountiesClaimed);

                    SendReply(player, hunterMessage);
                    return;
                case "wanted":
                    IEnumerable<PlayerData> top10Hunted = storedData.players.Values.OrderByDescending(x => x.totalWantedTime + x.GetCurrentWantedTime()).Take(10);
                    string wantedMessage = msg("top_wanted", player.userID);

                    for (int i = 0; i < top10Hunted.Count(); i++)
                    {
                        PlayerData playerData = top10Hunted.ElementAt(i);
                        wantedMessage += string.Format(msg("top_wanted_entry", player.userID), playerData.displayName, FormatTime(playerData.totalWantedTime + playerData.GetCurrentWantedTime()), playerData.totalBounties);

                        if (i == 4)
                        {
                            SendReply(player, wantedMessage);
                            wantedMessage = string.Empty;
                        }
                    }
                    //foreach (PlayerData playerData in top10Hunted)
                        //wantedMessage += string.Format(msg("top_wanted_entry", player.userID), playerData.displayName, FormatTime(playerData.totalWantedTime + playerData.GetCurrentWantedTime()), playerData.totalBounties);

                    SendReply(player, wantedMessage);
                    return;
                case "clear":
                    {
                        if (!player.IsAdmin && !permission.UserHasPermission(player.UserIDString, "bounty.admin"))
                            return;

                        if (args.Length < 2)
                        {
                            SendReply(player, msg("invalid_syntax", player.userID));
                            return;
                        }

                        IPlayer targetPlayer = covalence.Players.FindPlayer(args[1]);
                        if (targetPlayer == null)
                        {
                            SendReply(player, msg("no_player_found", player.userID));
                            return;
                        }

                        PlayerData playerData;
                        if (!storedData.players.TryGetValue(ulong.Parse(targetPlayer.Id), out playerData) || playerData.activeBounties.Count == 0)
                        {
                            SendReply(player, msg("no_active_bounties", player.userID));
                            return;
                        }

                        foreach(PlayerData.BountyInfo bounty in playerData.activeBounties)                        
                            storedData.rewards.Remove(bounty.rewardId);
                        playerData.activeBounties.Clear();
                        
                        SendReply(player, string.Format(msg("bounties_cleared", player.userID), targetPlayer.Name));
                    }
                    return;
                default:
                    SendReply(player, msg("invalid_syntax", player.userID));
                    break;
            }            
        }   
        
        [ConsoleCommand("bounty")]
        private void ccmdBounty(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null)
                return;

            if (arg.Args == null || arg.Args.Length == 0)
            {
                SendReply(arg, "bounty view <target name or ID> - View active bounties on the specified player");
                SendReply(arg, "bounty top - View the top 20 bounty hunters");
                SendReply(arg, "bounty wanted - View the top 20 most wanted players");
                SendReply(arg, "bounty clear <target name or ID> - Clear all active bounties on the specified player");
                SendReply(arg, "bounty wipe - Wipe all bounty data");
                return;
            }

            switch (arg.Args[0].ToLower())
            {
                case "view":
                    {
                        if (arg.Args.Length < 2)
                        {
                            SendReply(arg, "Invalid command syntax! Type 'bounty' to see available commands");
                            return;
                        }                        

                        IPlayer targetPlayer = covalence.Players.FindPlayer(arg.Args[1]);
                        if (targetPlayer == null)
                        {
                            SendReply(arg, "Unable to find a player with that name or ID");
                            return;
                        }

                        PlayerData playerData;
                        if (!storedData.players.TryGetValue(ulong.Parse(targetPlayer.Id), out playerData) || playerData.activeBounties.Count == 0)
                        {
                            SendReply(arg, "That player does not have any active bounties");
                            return;
                        }

                        SendReply(arg, string.Format("{0} has {1} active bounties", targetPlayer.Name, playerData.activeBounties.Count));
                        foreach (var bounty in playerData.activeBounties)
                        {
                            RewardInfo rewardInfo = storedData.rewards[bounty.rewardId];
                            string reward = string.Empty;
                            if (rewardInfo.rewardItems.Count > 1)
                            {
                                for (int i = 0; i < rewardInfo.rewardItems.Count; i++)
                                {
                                    RewardInfo.ItemData itemData = rewardInfo.rewardItems.ElementAt(i);
                                    reward += (string.Format("{0}x {1}", itemData.amount, idToDisplayName[itemData.itemid]) + (i < rewardInfo.rewardItems.Count - 1 ? ", " : ""));
                                }
                            }
                            else reward = rewardInfo.econAmount > 0 ? string.Format("{0} economics", rewardInfo.econAmount) : string.Format("{0} rp", rewardInfo.rpAmount);

                            SendReply(arg, string.Format("Placed by {0} {1} ago. Reward: {2}", bounty.initiatorName, FormatTime(CurrentTime() - bounty.initiatedTime), reward));
                        }
                    }
                    return;
                case "top":
                    IEnumerable<PlayerData> top20Hunters = storedData.players.Values.OrderByDescending(x => x.bountiesClaimed).Take(20);
                    string hunterMessage = "Top 20 Hunters:";

                    foreach (PlayerData playerData in top20Hunters)
                        hunterMessage += string.Format("\n{0} - {1} bounties collected", playerData.displayName, playerData.bountiesClaimed);

                    SendReply(arg, hunterMessage);
                    return;
                case "wanted":
                    IEnumerable<PlayerData> top20Hunted = storedData.players.Values.OrderByDescending(x => x.totalWantedTime + x.GetCurrentWantedTime()).Take(20);
                    string wantedMessage = "Top 20 Most Wanted:";

                    foreach (PlayerData playerData in top20Hunted)
                        wantedMessage += string.Format("\n{0} has all together been on the run for {1} with a total of {2} bounties", playerData.displayName, FormatTime(playerData.totalWantedTime + playerData.GetCurrentWantedTime()), playerData.totalBounties);

                    SendReply(arg, wantedMessage);
                    return;
                case "clear":
                    {
                        if (arg.Args.Length < 2)
                        {
                            SendReply(arg, "Invalid command syntax! Type 'bounty' to see available commands");
                            return;
                        }

                        IPlayer targetPlayer = covalence.Players.FindPlayer(arg.Args[1]);
                        if (targetPlayer == null)
                        {
                            SendReply(arg, "Unable to find a player with that name or ID");
                            return;
                        }

                        PlayerData playerData;
                        if (!storedData.players.TryGetValue(ulong.Parse(targetPlayer.Id), out playerData) || playerData.activeBounties.Count == 0)
                        {
                            SendReply(arg, "That player does not have any active bounties");
                            return;
                        }

                        foreach (var bounty in playerData.activeBounties)
                            storedData.rewards.Remove(bounty.rewardId);
                        playerData.activeBounties.Clear();

                        SendReply(arg, $"You have cleared all pending bounties from {targetPlayer.Name}");
                    }
                    return;
                case "wipe":
                    storedData.players.Clear();
                    storedData.rewards.Clear();
                    SaveData();
                    SendReply(arg, "All data has been wiped!");
                    return;
                default:
                    SendReply(arg, "Invalid command syntax! Type 'bounty' to see available commands");
                    break;
            }
        }
        #endregion        

        #region Config        
        private ConfigData configData;
        private class ConfigData
        {
            [JsonProperty(PropertyName = "Ignore kills by clan members")]
            public bool IgnoreClans { get; set; }
            [JsonProperty(PropertyName = "Ignore kills by friends")]
            public bool IgnoreFriends { get; set; }
            [JsonProperty(PropertyName = "Notification Options")]
            public NotificationOptions Notifications { get; set; }
            [JsonProperty(PropertyName = "Reward Options")]
            public RewardOptions Rewards { get; set; }

            public class NotificationOptions
            {
                [JsonProperty(PropertyName = "PopupNotifications - Broadcast using PopupNotifications")]
                public bool UsePopupNotifications { get; set; }
                [JsonProperty(PropertyName = "PopupNotifications - Duration of notification")]
                public float PopupDuration { get; set; }
                [JsonProperty(PropertyName = "Broadcast new bounties globally")]
                public bool BroadcastNewBounties { get; set; }
                [JsonProperty(PropertyName = "Reminders - Remind targets they have a bounty on them")]
                public bool ShowReminders { get; set; }
                [JsonProperty(PropertyName = "Reminders - Amount of time between reminders (in minutes)")]
                public int ReminderTime { get; set; }
            }

            public class RewardOptions
            {
                [JsonProperty(PropertyName = "Allow bounties to be placed using Economics")]
                public bool AllowEconomics { get; set; }
                [JsonProperty(PropertyName = "Allow bounties to be placed using RP")]
                public bool AllowServerRewards { get; set; }
                [JsonProperty(PropertyName = "Allow bounties to be placed using items")]
                public bool AllowItems { get; set; }
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
                IgnoreClans = true,
                IgnoreFriends = true,
                Notifications = new ConfigData.NotificationOptions
                {
                    BroadcastNewBounties = true,
                    PopupDuration = 8f,
                    ReminderTime = 30,
                    ShowReminders = true,
                    UsePopupNotifications = false
                },
                Rewards = new ConfigData.RewardOptions
                {
                    AllowEconomics = true,
                    AllowItems = true,
                    AllowServerRewards = true
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

        #region Data Management        
        private void SaveData() => data.WriteObject(storedData);        
        
        private void LoadData()
        {
            try
            {
                storedData = data.ReadObject<StoredData>();
            }
            catch
            {
                storedData = new StoredData();
            }
        }
       
        private class StoredData
        {
            public Dictionary<ulong, PlayerData> players = new Dictionary<ulong, PlayerData>();
            public Dictionary<int, RewardInfo> rewards = new Dictionary<int, RewardInfo>();
        }

        private class PlayerData
        {
            public string displayName;
            public int totalBounties;
            public int bountiesClaimed;
            public double totalWantedTime;
            public List<BountyInfo> activeBounties = new List<BountyInfo>();
            public List<int> unclaimedRewards = new List<int>();

            public PlayerData() { }

            public PlayerData(string displayName)
            {
                this.displayName = displayName;
            }
            
            public void ClaimRewards(List<int> rewards)
            {
                foreach(int reward in rewards)
                {
                    unclaimedRewards.Add(reward);
                    bountiesClaimed++;
                }
            }

            public void UpdateWantedTime()
            {                
                totalWantedTime += GetCurrentWantedTime();
            }

            public double GetCurrentWantedTime()
            {
                double largestTime = 0;
                foreach (BountyInfo bountyInfo in activeBounties)
                {
                    double time = DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1, 0, 0, 0)).TotalSeconds - bountyInfo.initiatedTime;
                    if (time > largestTime)
                        largestTime = time;
                }
                return largestTime;
            }

            public class BountyInfo
            {
                public ulong initiatorId;
                public string initiatorName;
                public double initiatedTime;
                public int rewardId;

                public BountyInfo() { }
                public BountyInfo(ulong initiatorId, string initiatorName, int rewardId)
                {
                    this.initiatorId = initiatorId;
                    this.initiatorName = initiatorName;
                    this.rewardId = rewardId;
                    this.initiatedTime = DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1, 0, 0, 0)).TotalSeconds;
                }                
            }
        }

        private class RewardInfo
        {
            public int rpAmount;
            public int econAmount;
            public List<ItemData> rewardItems = new List<ItemData>();

            public RewardInfo() { }
            public RewardInfo(int rpAmount, int econAmount, ItemContainer container)
            {
                this.rpAmount = rpAmount;
                this.econAmount = econAmount;
                if (container != null)                
                    rewardItems = GetItems(container).ToList();                
            }

            private IEnumerable<ItemData> GetItems(ItemContainer container)
            {
                return container.itemList.Select(item => new ItemData
                {
                    itemid = item.info.itemid,
                    amount = item.amount,
                    ammo = (item.GetHeldEntity() as BaseProjectile)?.primaryMagazine.contents ?? 0,
                    ammotype = (item.GetHeldEntity() as BaseProjectile)?.primaryMagazine.ammoType.shortname ?? null,
                    skin = item.skin,
                    condition = item.condition,
                    instanceData = new ItemData.InstanceData(item),
                    contents = item.contents?.itemList.Select(item1 => new ItemData
                    {
                        itemid = item1.info.itemid,
                        amount = item1.amount,
                        condition = item1.condition
                    }).ToArray()
                });
            }

            public class ItemData
            {
                public int itemid;
                public ulong skin;
                public int amount;
                public float condition;
                public int ammo;
                public string ammotype;
                public InstanceData instanceData;
                public ItemData[] contents;

                public class InstanceData
                {
                    public int dataInt;
                    public int blueprintTarget;
                    public int blueprintAmount;

                    public InstanceData() { }
                    public InstanceData(Item item)
                    {
                        if (item.instanceData == null)
                            return;

                        dataInt = item.instanceData.dataInt;
                        blueprintAmount = item.instanceData.blueprintAmount;
                        blueprintTarget = item.instanceData.blueprintTarget;
                    }

                    public void Restore(Item item)
                    {
                        item.instanceData = new ProtoBuf.Item.InstanceData();
                        item.instanceData.blueprintAmount = blueprintAmount;
                        item.instanceData.blueprintTarget = blueprintTarget;
                        item.instanceData.dataInt = dataInt;
                    }
                }
            }
        }         
        #endregion

        #region Localization
        private string msg(string key, ulong playerId = 0U) => lang.GetMessage(key, this, playerId == 0U ? null : playerId.ToString());

        Dictionary<string, string> messages = new Dictionary<string, string>
        {
            ["rewards_pending"] = "<color=#D3D3D3><color=#ce422b>{0}</color> had <color=#ce422b>{1}</color> outstanding bounties on them. You can claim your rewards by typing</color> <color=#ce422b>/bounty</color>",
            ["is_friend"] = "<color=#D3D3D3>You cannot claim a bounty on a friend or clan mate</color>",
            ["bounty_placed_target"] = "<color=#ce422b>{0} </color><color=#D3D3D3>has placed a bounty on you</color>",
            ["bounty_placed_initiator"] = "<color=#D3D3D3>You have successfully placed a bounty on</color> <color=#ce422b>{0}</color>",
            ["no_bounty_placed"] = "<color=#D3D3D3>You do not have a bounty placed on </color> <color=#ce422b>{0}</color>",
            ["bounty_cancelled_target"] = "<color=#ce422b>{0} </color><color=#D3D3D3>has cancelled their bounty on you</color>",
            ["bounty_cancelled_initiator"] = "<color=#D3D3D3>You have cancelled the bounty on</color> <color=#ce422b>{0}</color>",
            ["no_permission"] = "<color=#D3D3D3>You do not have permission to use that command</color>",
            ["no_items_deposited"] = "<color=#D3D3D3>You did not place any items in the box</color>",
            ["invalid_syntax"] = "<color=#D3D3D3>Invalid command syntax! Type <color=#ce422b>/bounty</color> to see available commands</color>",
            ["help1"] = "<color=#ce422b>/bounty add items <target name or ID></color><color=#D3D3D3> - Create a new bounty using items as the reward</color>",
            ["help2"] = "<color=#ce422b>/bounty add rp <target name or ID> <amount></color><color=#D3D3D3> - Create a new bounty using RP as the reward</color>",
            ["help3"] = "<color=#ce422b>/bounty add eco <target name or ID> <amount></color><color=#D3D3D3> - Create a new bounty using RP as the reward</color>",
            ["help4"] = "<color=#ce422b>/bounty cancel <target name or ID></color><color=#D3D3D3> - Cancel a bounty you placed on a player</color>",
            ["help5"] = "<color=#ce422b>/bounty claim</color><color=#D3D3D3> - Claim a outstanding reward</color>",
            ["help6"] = "<color=#ce422b>/bounty view <target name or ID></color><color=#D3D3D3> - View active bounties on on the target player</color>",
            ["help7"] = "<color=#ce422b>/bounty top</color><color=#D3D3D3> - List the top 10 bounty hunters</color>",
            ["help8"] = "<color=#ce422b>/bounty wanted</color><color=#D3D3D3> - List the top 10 most wanted players</color>",
            ["help9"] = "<color=#ce422b>/bounty clear <target name or ID></color><color=#D3D3D3> - Clear all bounties on the target player</color>",
            ["no_player_found"] = "<color=#D3D3D3>Unable to find a player with that name or ID</color>",
            ["multiple_players_found"] = "<color=#D3D3D3>Multiple players found with that name</color>",
            ["has_active_bounty"] = "<color=#D3D3D3>You already have a active bounty on that player</color>",
            ["no_value_entered"] = "<color=#D3D3D3>You must enter an amount</color>",
            ["not_enough_rp"] = "<color=#D3D3D3>You do not have enough RP to place this bounty</color>",
            ["not_enough_eco"] = "<color=#D3D3D3>You do not have enough money to place this bounty</color>",
            ["no_active_bounties"] = "<color=#D3D3D3>That player does not have any active bounties</color>",
            ["player_has_bounties"] = "<color=#D3D3D3><color=#ce422b>{0}</color> has <color=#ce422b>{1}</color> active bounties</color>",
            ["bounty_info"] = "<color=#D3D3D3>Placed by <color=#ce422b>{0} {1}</color> ago. Reward: </color><color=#ce422b>{2}</color>",
            ["reward_econ"] = "<color=#ce422b>{0}</color> <color=#D3D3D3>economics</color>",
            ["reward_rp"] = "<color=#ce422b>{0}</color> <color=#D3D3D3>rp</color>",
            ["reward_item"] = "<color=#D3D3D3>{0} x</color> <color=#ce422b>{1}</color>",
            ["help10"] = "<color=#ce422b>/bounty claim <ID></color><color=#D3D3D3> - Claim the reward for the bounty with the specified ID number</color>",
            ["no_rewards_pending"] = "<color=#D3D3D3>You do not have any outstanding rewards to be claimed</color>",
            ["reward_info"] = "<color=#D3D3D3>ID: <color=#ce422b>{0}</color> - Reward: </color><color=#ce422b>{1}</color>",
            ["top_hunters"] = "<color=#ce422b>Top 10 Hunters:</color>",
            ["top_hunter_entry"] = "<color=#D3D3D3>\n<color=#ce422b>{0}</color> - <color=#ce422b>{1}</color> bounties collected</color>",
            ["top_wanted"] = "<color=#ce422b>Top 10 Most Wanted:</color>",
            ["top_wanted_entry"] = "<color=#D3D3D3>\n<color=#ce422b>{0}</color> has all together been on the run for <color=#ce422b>{1}</color> with a total of <color=#ce422b>{2}</color> bounties</color>",
            ["cant_bounty_self"] = "<color=#D3D3D3>You can not place a bounty on yourself</color>",
            ["title"] = "<color=#ce422b>{0}  <color=#D3D3D3>v</color>{1}</color>",
            ["bounties_outstanding"] = "<color=#D3D3D3>You have <color=#ce422b>{0}</color> active bounties on you!</color>",
            ["bounties_cleared"] = "<color=#D3D3D3>You have cleared all pending bounties from </color><color=#ce422b>{0}</color>",
            ["reward_claimed"] = "<color=#D3D3D3>You have claimed your reward!</color>"
        };        
        #endregion

    }
}
