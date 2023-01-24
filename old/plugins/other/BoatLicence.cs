using Oxide.Core;
using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Boat Licence", "Sorrow", "0.6.3")]
    [Description("Allows players to buy a boat and then spawn or store it")]

    class BoatLicence : RustPlugin
    {
        #region Fields
        [PluginReference]
        Plugin Economics, ServerRewards;

        private Dictionary<ulong, LisencedPlayer> _lisencedPlayer = new Dictionary<ulong, LisencedPlayer>();
        private Dictionary<uint, LisencedPlayer> _boatsCache = new Dictionary<uint, LisencedPlayer>();

        private bool _canBuyRowBoat;
        private bool _canBuyRhibBoat;

        private double _rowBoatCost;
        private double _rhibBoatCost;

        private int _intervalToCheckBoat;
        private int _timeBeforeBoatWipe;
        private double _cooldownToUseSpawnCmd;

        private bool _useEconomics;
        private bool _useServerRewards;
        private string _itemsNeededToBuyBoat;

        const string prefix = "<color='orange'>[Boat Licence]</color> ";
        const string rowBoatPrefab = "assets/content/vehicles/boats/rowboat/rowboat.prefab";
        const string rhibBoatPrefab = "assets/content/vehicles/boats/rhib/rhib.prefab";
        const string scrap = "scrap";
        #endregion

        #region uMod Hooks
        private void OnServerInitialized()
        {
            _canBuyRowBoat = Convert.ToBoolean(Config["Can buy rowing boat"]);
            _canBuyRhibBoat = Convert.ToBoolean(Config["Can buy RHIB"]);

            _rowBoatCost = Convert.ToDouble(Config["Cost of rowing boat"]);
            _rhibBoatCost = Convert.ToDouble(Config["Cost of RHIB"]);

            _intervalToCheckBoat = Convert.ToInt32(Config["Interval in minutes to check boat for wipe"]);
            _timeBeforeBoatWipe = Convert.ToInt32(Config["Time before boat wipe in minutes"]);
            _cooldownToUseSpawnCmd = Convert.ToDouble(Config["Cooldown time in seconds to use the chat command to spawn a boat"]);

            _useEconomics = Convert.ToBoolean(Config["Use Economics to buy boat"]);
            _useServerRewards = Convert.ToBoolean(Config["Use ServerRewards to buy boat"]);
            _itemsNeededToBuyBoat = Convert.ToString(Config["Shortname of item needed to buy boat"]);

            if (Economics == null && _useEconomics)
            {
                PrintWarning("Economics is not loaded, get it at https://umod.org");
            }
            if(ServerRewards == null && _useServerRewards)
            {
                PrintWarning("ServerRewards is not loaded, get it at https://umod.org");
            }

            LoadData();
            CheckBoats();
        }

        private void Unload()
        {
            foreach (var boat in _boatsCache.ToList())
            {
                RemoveBoat(boat.Key, boat.Value);
            }

            SaveData();
        }

        private void OnServerSave()
        {
            SaveData();
        }

        private void OnServerShutdown()
        {
            SaveData();
        }

        private void OnEntityDismounted(BaseMountable entity, BasePlayer player)
        {
            var vehicleParent = entity?.VehicleParent();
            if (vehicleParent == null) return;

            LisencedPlayer lisencedPlayer;
            if (!_boatsCache.TryGetValue(vehicleParent.net.ID, out lisencedPlayer)) return;
            lisencedPlayer.UpdateBoatLastDismount(vehicleParent.net.ID);
        }

        private void OnEntityKill(BaseNetworkable entity)
        {
            if (entity == null || entity.net?.ID == null) return;
            LisencedPlayer lisencedPlayer;
            if (!_boatsCache.TryGetValue(entity.net.ID, out lisencedPlayer)) return;
            lisencedPlayer.ResetBoat(entity.net.ID);
            _boatsCache.Remove(entity.net.ID);
        }
        #endregion

        #region Commands
        /// <summary>
        /// Commands the boat licence.
        /// </summary>
        /// <param name="player">The player.</param>
        /// <param name="command">The command.</param>
        /// <param name="args">The arguments.</param>
        [ChatCommand("boathelp")]
        void CmdBoatHelp(BasePlayer player, string command, string[] args)
        {
            SendReply(player, Msg("helpBoat", player.UserIDString));
        }

        /// <summary>
        /// Commands the buy row boat.
        /// </summary>
        /// <param name="player">The player.</param>
        /// <param name="command">The command.</param>
        /// <param name="args">The arguments.</param>
        [ChatCommand("buyboat")]
        void CmdBuyBoat(BasePlayer player, string command, string[] args)
        {
            LisencedPlayer lisencedPlayer;

            if (args.Length == 0) SendReply(player, Msg("helpBuyBoat", player.UserIDString));
            if (args.Length >= 1)
            {
                switch (args[0].ToLower())
                {
                    case "row":
                        if (!_canBuyRowBoat)
                        {
                            SendReply(player, Msg("rowBoatCannotBeBuyed", player.UserIDString));
                            break;
                        }
                        if (!_lisencedPlayer.TryGetValue(player.userID, out lisencedPlayer))
                        {
                            lisencedPlayer = new LisencedPlayer(player.userID);
                            _lisencedPlayer.Add(player.userID, lisencedPlayer);
                            if (Economics != null && _useEconomics && Economics.Call<bool>("Withdraw", player.userID, _rowBoatCost)
                                || ServerRewards != null && _useServerRewards && ServerRewards.Call<bool>("TakePoints", player.userID, _rowBoatCost)
                                || !_useEconomics && !_useServerRewards && Withdraw(player, _rowBoatCost))
                            {
                                lisencedPlayer.rowBoat.Buyed = true;
                                SendReply(player, Msg("boatPurchased", player.UserIDString));
                            }
                            else
                            {
                                SendReply(player, Msg("noMoney", player.UserIDString));
                            }
                        }
                        else if (!lisencedPlayer.rowBoat.Buyed)
                        {
                            if (Economics != null && _useEconomics && Economics.Call<bool>("Withdraw", player.userID, _rowBoatCost)
                                || ServerRewards != null && _useServerRewards && ServerRewards.Call<bool>("TakePoints", player.userID, _rowBoatCost)
                                || !_useEconomics && !_useServerRewards && Withdraw(player, _rowBoatCost))
                            {
                                lisencedPlayer.rowBoat.Buyed = true;
                                SendReply(player, Msg("boatPurchased", player.UserIDString));
                            }
                            else
                            {
                                SendReply(player, Msg("noMoney", player.UserIDString));
                            }
                        }
                        else
                        {
                            SendReply(player, Msg("boatAlreadyPurchased", player.UserIDString));
                        }
                        break;
                    case "rhib":
                        if (!_canBuyRhibBoat)
                        {
                            SendReply(player, Msg("rhibCannotBeBuyed", player.UserIDString));
                            break;
                        }
                        if (!_lisencedPlayer.TryGetValue(player.userID, out lisencedPlayer))
                        {
                            lisencedPlayer = new LisencedPlayer(player.userID);
                            _lisencedPlayer.Add(player.userID, lisencedPlayer);
                            if (Economics != null && _useEconomics && Economics.Call<bool>("Withdraw", player.userID, _rhibBoatCost)
                                || ServerRewards != null && _useServerRewards && ServerRewards.Call<bool>("TakePoints", player.userID, _rhibBoatCost)
                                || !_useEconomics && !_useServerRewards && Withdraw(player, _rhibBoatCost))
                            {
                                lisencedPlayer.rhibBoat.Buyed = true;
                                SendReply(player, Msg("boatPurchased", player.UserIDString));
                            }
                            else
                            {
                                SendReply(player, Msg("noMoney", player.UserIDString));
                            }
                        }
                        else if (!lisencedPlayer.rhibBoat.Buyed)
                        {
                            if (Economics != null && _useEconomics && Economics.Call<bool>("Withdraw", player.userID, _rhibBoatCost)
                                || ServerRewards != null && _useServerRewards && ServerRewards.Call<bool>("TakePoints", player.userID, _rhibBoatCost)
                                || !_useEconomics && !_useServerRewards && Withdraw(player, _rhibBoatCost))
                            {
                                lisencedPlayer.rhibBoat.Buyed = true;
                                SendReply(player, Msg("boatPurchased", player.UserIDString));
                            }
                            else
                            {
                                SendReply(player, Msg("noMoney", player.UserIDString));
                            }
                        }
                        else
                        {
                            SendReply(player, Msg("boatAlreadyPurchased", player.UserIDString));
                        }
                        break;
                    default:
                        SendReply(player, Msg("helpOptionNotFound", player.UserIDString));
                        break;
                }
            }
        }

        /// <summary>
        /// Commands the spawn row boat.
        /// </summary>
        /// <param name="player">The player.</param>
        /// <param name="command">The command.</param>
        /// <param name="args">The arguments.</param>
        [ChatCommand("spawnboat")]
        void CmdSpawnBoat(BasePlayer player, string command, string[] args)
        {
            if (!IsInWater(player))
            {
                SendReply(player, Msg("notInWater", player.UserIDString));
                return;
            }
            if (player.IsBuildingBlocked())
            {
                SendReply(player, Msg("buildindBlocked", player.UserIDString));
                return;
            }

            Vector3 position = player.transform.position + (player.transform.forward * 3);
            LisencedPlayer lisencedPlayer;

            if (args.Length == 0) SendReply(player, Msg("helpSpawnBoat", player.UserIDString));
            if (args.Length >= 1)
            {
                switch (args[0].ToLower())
                {
                    case "row":
                        if (_lisencedPlayer.TryGetValue(player.userID, out lisencedPlayer))
                        {
                            if (!lisencedPlayer.rowBoat.Buyed)
                            {
                                SendReply(player, Msg("didntBuyRowBoat", player.UserIDString));
                                return;
                            }
                            else if (lisencedPlayer.rowBoat.Id != 0)
                            {
                                SendReply(player, Msg("alreadyRowBoatOut", player.UserIDString));
                                return;
                            }
                            else if (_cooldownToUseSpawnCmd > 0 && lisencedPlayer.boatSpawned > (DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0)).Subtract(TimeSpan.FromSeconds(_cooldownToUseSpawnCmd)))
                            {
                                SendReply(player, string.Format(Msg("boatOnCooldown", player.UserIDString), Convert.ToInt32((lisencedPlayer.boatSpawned - (DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0)).Subtract(TimeSpan.FromSeconds(_cooldownToUseSpawnCmd))).TotalSeconds)));
                                return;
                            }
                            var entity = SpawnBoat(rowBoatPrefab, position, player.transform.rotation);
                            if (entity == null) return;
                            lisencedPlayer.boatSpawned = (DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0));
                            lisencedPlayer.rowBoat.Id = entity.net.ID;
                            lisencedPlayer.UpdateBoatLastDismount(entity.net.ID);
                            _boatsCache.Add(lisencedPlayer.rowBoat.Id, lisencedPlayer);
                            SendReply(player, Msg("boatSpawned", player.UserIDString));
                        }
                        else
                        {
                            SendReply(player, Msg("boatNotYetPurchased", player.UserIDString));
                        }
                        break;
                    case "rhib":
                        if (_lisencedPlayer.TryGetValue(player.userID, out lisencedPlayer))
                        {
                            if (!lisencedPlayer.rhibBoat.Buyed)
                            {
                                SendReply(player, Msg("didntBuyRhib", player.UserIDString));
                                return;
                            }
                            else if (lisencedPlayer.rhibBoat.Id != 0)
                            {
                                SendReply(player, Msg("alreadyRhibOut", player.UserIDString));
                                return;
                            }
                            else if (_cooldownToUseSpawnCmd > 0 && lisencedPlayer.boatSpawned > DateTime.Now.Subtract(TimeSpan.FromSeconds(_cooldownToUseSpawnCmd)).TimeOfDay)
                            {
                                SendReply(player, string.Format(Msg("boatOnCooldown", player.UserIDString), Convert.ToInt32((lisencedPlayer.boatSpawned - DateTime.Now.Subtract(TimeSpan.FromSeconds(_cooldownToUseSpawnCmd)).TimeOfDay).TotalSeconds)));
                                return;
                            }
                            var entity = SpawnBoat(rhibBoatPrefab, position, player.transform.rotation);
                            if (entity == null) return;
                            lisencedPlayer.boatSpawned = DateTime.Now.TimeOfDay;
                            lisencedPlayer.rhibBoat.Id = entity.net.ID;
                            lisencedPlayer.UpdateBoatLastDismount(entity.net.ID);
                            _boatsCache.Add(lisencedPlayer.rhibBoat.Id, lisencedPlayer);
                            SendReply(player, Msg("boatSpawned", player.UserIDString));
                        }
                        else
                        {
                            SendReply(player, Msg("boatNotYetPurchased", player.UserIDString));
                        }
                        break;
                    default:
                        SendReply(player, Msg("helpOptionNotFound", player.UserIDString));
                        break;
                }
            }
        }

        /// <summary>
        /// Commands the recall row boat.
        /// </summary>
        /// <param name="player">The player.</param>
        /// <param name="command">The command.</param>
        /// <param name="args">The arguments.</param>
        [ChatCommand("recallboat")]
        void CmdRecallBoat(BasePlayer player, string command, string[] args)
        {
            LisencedPlayer lisencedPlayer;

            if (args.Length == 0) SendReply(player, Msg("helpRecallBoat", player.UserIDString));
            if (args.Length >= 1)
            {
                switch (args[0].ToLower())
                {
                    case "row":
                        if (_lisencedPlayer.TryGetValue(player.userID, out lisencedPlayer))
                        {
                            RemoveBoat(lisencedPlayer.rowBoat.Id, lisencedPlayer);
                            SendReply(player, Msg("boatRecalled", player.UserIDString));
                        }
                        break;
                    case "rhib":
                        if (_lisencedPlayer.TryGetValue(player.userID, out lisencedPlayer))
                        {
                            RemoveBoat(lisencedPlayer.rhibBoat.Id, lisencedPlayer);
                            SendReply(player, Msg("boatRecalled", player.UserIDString));
                        }
                        break;
                    default:
                        SendReply(player, Msg("helpOptionNotFound", player.UserIDString));
                        break;
                }
            }
        }
        #endregion

        #region Functions
        /// <summary>
        /// Withdraws the specified player.
        /// </summary>
        /// <param name="player">The player.</param>
        /// <param name="price">The price.</param>
        /// <returns></returns>
        private bool Withdraw(BasePlayer player, double price)
        {
            var item = ItemManager.FindItemDefinition(_itemsNeededToBuyBoat);
            if (item == null) return false;
            if (player.inventory.GetAmount(item.itemid) >= price)
            {
                player.inventory.Take(null, item.itemid, (int)price);
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Spawns the boat.
        /// </summary>
        /// <param name="prefab">The prefab.</param>
        /// <param name="position">The position.</param>
        /// <param name="rotation">The rotation.</param>
        /// <returns></returns>
        private BaseEntity SpawnBoat(string prefab, Vector3 position, Quaternion rotation = default(Quaternion))
        {
            BaseEntity entity = GameManager.server.CreateEntity(prefab, position + Vector3.up, rotation);
            if (entity == null) return null;
            entity.enableSaving = true;
            entity.Spawn();

            return entity;
        }

        /// <summary>
        /// Removes the boat.
        /// </summary>
        /// <param name="boatID">The boat identifier.</param>
        /// <param name="lisencedPlayer">The lisenced player.</param>
        private void RemoveBoat(uint boatID, LisencedPlayer lisencedPlayer)
        {
            lisencedPlayer.ResetBoat(boatID);
            _boatsCache.Remove(boatID);
            BaseNetworkable.serverEntities.Find(boatID)?.Kill();
        }

        /// <summary>
        /// Updates the lisenced player.
        /// </summary>
        /// <param name="lisencedPlayer">The lisenced player.</param>
        /// <param name="boatID">The boat identifier.</param>
        private void UpdateLisencedPlayer(LisencedPlayer lisencedPlayer, uint boatID)
        {
            if (lisencedPlayer.rowBoat.Id == boatID) lisencedPlayer.rowBoat.Id = 0;
            if (lisencedPlayer.rhibBoat.Id == boatID) lisencedPlayer.rhibBoat.Id = 0;
        }

        /// <summary>
        /// Checks the boats.
        /// </summary>
        private void CheckBoats()
        {
            foreach (var boat in _boatsCache.ToList())
            {
                LisencedPlayer lisencedPlayer = boat.Value;
                var boatNetworkable = BaseNetworkable.serverEntities.Find(boat.Key);
                if (boatNetworkable == null) continue;
                var boatEntity = boatNetworkable.GetComponent<BaseBoat>();
                if (boatEntity == null) continue;
                if (boatEntity.IsMounted()) continue;
                if (lisencedPlayer.rowBoat.Id == boat.Key)
                {
                    if (BoatIsActive(lisencedPlayer.rowBoat.LastDismount)) continue;
                    RemoveBoat(boat.Key, lisencedPlayer);
                    var player = BasePlayer.FindByID(lisencedPlayer.Userid);
                    if (player == null) continue;
                    SendReply(player, Msg("boatRecalled", player.UserIDString));
                }
                else if (lisencedPlayer.rhibBoat.Id == boat.Key)
                {
                    if (BoatIsActive(lisencedPlayer.rhibBoat.LastDismount)) continue;
                    RemoveBoat(boat.Key, lisencedPlayer);
                    var player = BasePlayer.FindByID(lisencedPlayer.Userid);
                    if (player == null) continue;
                    SendReply(player, Msg("boatRecalled", player.UserIDString));
                }
            }

            timer.Once(_intervalToCheckBoat * 60f, () => CheckBoats());
        }

        /// <summary>
        /// Boats is active.
        /// </summary>
        /// <param name="lastDismount">The last dismount.</param>
        /// <returns></returns>
        private bool BoatIsActive(DateTime lastDismount)
        {
            return lastDismount.Ticks >= DateTime.UtcNow.Subtract(TimeSpan.FromMinutes(_timeBeforeBoatWipe)).Ticks;
        }

        /// <summary>
        /// Determines whether [is in water] [the specified player].
        /// </summary>
        /// <param name="player">The player.</param>
        /// <returns>
        ///   <c>true</c> if [is in water] [the specified player]; otherwise, <c>false</c>.
        /// </returns>
        private bool IsInWater(BasePlayer player)
        {
            var modelState = player.modelState;
            return modelState != null && modelState.waterLevel > 0f && player.metabolism.wetness.value > 0f;
        }
        #endregion

        #region Localization
        /// <summary>
        /// MSGs the specified key.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="playerId">The player identifier.</param>
        /// <returns></returns>
        private string Msg(string key, string playerId = null) => prefix + lang.GetMessage(key, this, playerId);

        /// <summary>
        /// Loads the default messages.
        /// </summary>
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["helpBoat"] = "These are the available commands: \n<color='green'>/buyboat</color> -- To buy a boat \n<color='green'>/spawnboat</color> -- To spawn a boat \n<color='green'>/recallboat</color> -- To recall a boat",
                ["helpBuyBoat"] = "These are the available commands: \n<color='green'>/buyboat row</color> -- To buy a rowing boat \n<color='green'>/buyboat rhib</color> -- To buy a RHIB.",
                ["helpSpawnBoat"] = "These are the available commands: \n<color='green'>/spawnboat row</color> -- To spawn a rowing boat \n<color='green'>/spawnboat rhib</color> -- To spawn a RHIB.",
                ["helpRecallBoat"] = "These are the available commands: \n<color='green'>/recallboat row</color> -- To recall a rowing boat \n<color='green'>/recallboat rhib</color> -- To recall a RHIB.",
                ["helpOptionNotFound"] = "This option doesn't exist.",
                ["boatPurchased"] = "You have purchased a boat, type <color='green'>/spawnboat</color> for more information.",
                ["boatAlreadyPurchased"] = "You have already purchased this boat.",
                ["rowBoatCannotBeBuyed"] = "You can't buy a rowing boat.",
                ["rhibCannotBeBuyed"] = "You can't buy a RHIB.",
                ["noMoney"] = "You don't have enough money.",
                ["didntBuyRowBoat"] = "You didn't purchase a rowing boat.",
                ["didntBuyRhib"] = "You didn't purchase a RHIB.",
                ["alreadyRowBoatOut"] = "You already have a rowing boat outside, type <color='green'>/spawnboat</color> for more information.",
                ["alreadyRhibOut"] = "You already have a RHIB outside, type <color='green'>/spawnboat</color> for more information.",
                ["boatNotYetPurchased"] = "You have not yet purchased a boat.",
                ["boatSpawned"] = "You spawned your boat.",
                ["boatRecalled"] = "You recalled your boat.",
                ["boatOnCooldown"] = "You must wait {0} seconds before you can spawn your boat.",
                ["notInWater"] = "You must be in the water to use this command.",
                ["buildindBlocked"] = "You can't spawn a boat appear if you don't have the building privileges.",
            }, this);

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["helpBoat"] = "Voici les commandes disponibles : \n<color='green'>/buyboat</color> -- Pour acheter un bateau \n<color='green'>/spawnboat</color> -- Pour faire apparaître un bateau \n<color='green'>/recallboat</color> -- Pour ranger un bateau",
                ["helpBuyBoat"] = "Voici les commandes disponibles : \n<color='green'>/buyboat row</color> -- Pour acheter un bateau à rames \n<color='green'>/buyboat rhib</color> -- Pour acheter un RHIB.",
                ["helpSpawnBoat"] = "Voici les commandes disponibles : \n<color='green'>/spawnboat row</color> -- Pour faire apparaître un bateau à rames \n<color='green'>/spawnboat rhib</color> -- Pour faire apparaître un RHIB.",
                ["helpRecallBoat"] = "Voici les commandes disponibles : \n<color='green'>/recallboat row</color> -- Pour ranger un bateau à rames \n<color='green'>/recallboat rhib</color> -- Pour ranger un RHIB.",
                ["helpOptionNotFound"] = "Cette option n'existe pas.",
                ["boatPurchased"] = "Vous avez acheté un bateau, tapez <color='green'>/spawnboat</color> pour plus d'informations",
                ["boatAlreadyPurchased"] = "Vous avez déjà acheté ce bateau.",
                ["rowBoatCannotBeBuyed"] = "On ne peut pas acheter un bateau à rames.",
                ["rhibCannotBeBuyed"] = "Vous ne pouvez pas acheter un RHIB.",
                ["noMoney"] = "Vous n'avez pas assez d'argent.",
                ["didntBuyRowBoat"] = "Vous n'avez pas acheté de bateau à rames.",
                ["didntBuyRhib"] = "Vous n'avez pas acheté de RHIB.",
                ["alreadyRowBoatOut"] = "Vous avez déjà un bateau à rames à l'extérieur, tapez <color='green'>/recallboat</color> pour plus d'informations.",
                ["alreadyRhibOut"] = "Vous avez déjà un RHIB à l'extérieur, tapez <color='green'>/recallboat</color> pour plus d'informations.",
                ["boatNotYetPurchased"] = "Vous n'avez pas encore acheté de bateau.",
                ["boatSpawned"] = "Vous avez fait apparaître votre bateau.",
                ["boatRecalled"] = "Vous avez rangé votre bateau.",
                ["boatOnCooldown"] = "Vous devez attendre {0} secondes avant de pouvoir faire apparaître votre bateau.",
                ["notInWater"] = "Vous devez être dans l'eau pour utiliser cette commande.",
                ["buildindBlocked"] = "Vous ne pouvez pas faire apparaître un bateau si vous n'avez pas les privilèges de construction.",
            }, this, "fr");
        }
        #endregion

        #region Config
        /// <summary>
        /// Loads the default configuration.
        /// </summary>
        private new void LoadDefaultConfig()
        {
            PrintWarning("Creating a new configuration file");
            Config.Clear();

            Config["Can buy rowing boat"] = true;
            Config["Can buy RHIB"] = true;
            Config["Cost of rowing boat"] = 1000;
            Config["Cost of RHIB"] = 5000;
            Config["Interval in minutes to check boat for wipe"] = 5;
            Config["Time before boat wipe in minutes"] = 15;
            Config["Use Economics to buy boat"] = false;
            Config["Use ServerRewards to buy boat"] = false;
            Config["Shortname of item needed to buy boat"] = scrap;
            Config["Cooldown time in seconds to use the chat command to spawn a boat"] = 60;

            SaveConfig();
        }

        /// <summary>
        /// Loads the data.
        /// </summary>
        private void LoadData()
        {
            _lisencedPlayer = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, LisencedPlayer>>("BoatLicence");
        }

        /// <summary>
        /// Saves the data.
        /// </summary>
        private void SaveData() => Interface.Oxide.DataFileSystem.WriteObject("BoatLicence", _lisencedPlayer);
        #endregion

        #region Class
        class LisencedPlayer
        {
            private readonly ulong userid;
            public TimeSpan boatSpawned { get; set; }
            public RowBoat rowBoat { get; set; }
            public RhibBoat rhibBoat { get; set; }

            public LisencedPlayer(ulong userid)
            {
                this.userid = userid;
                rowBoat = new RowBoat();
                rhibBoat = new RhibBoat();
            }

            public ulong Userid
            {
                get
                {
                    return userid;
                }
            }

            public void ResetBoat(uint boatID)
            {
                if (rowBoat.Id == boatID)
                {
                    rowBoat.Id = 0;
                    rowBoat.LastDismount = DateTime.MinValue;
                }
                else if (rhibBoat.Id == boatID)
                {
                    rhibBoat.Id = 0;
                    rhibBoat.LastDismount = DateTime.MinValue;
                }
            }

            public void UpdateBoatLastDismount(uint boatID)
            {
                if (rowBoat.Id == boatID) rowBoat.LastDismount = DateTime.UtcNow;
                if (rhibBoat.Id == boatID) rhibBoat.LastDismount = DateTime.UtcNow;
            }
        }

        class RowBoat
        {
            private bool buyed;
            private uint id;
            private DateTime lastDismount;

            public RowBoat()
            {
                LastDismount = DateTime.MinValue;
            }

            public bool Buyed
            {
                get
                {
                    return buyed;
                }

                set
                {
                    buyed = value;
                }
            }

            public uint Id
            {
                get
                {
                    return id;
                }

                set
                {
                    id = value;
                }
            }

            public DateTime LastDismount
            {
                get
                {
                    return lastDismount;
                }

                set
                {
                    lastDismount = value;
                }
            }
        }

        class RhibBoat
        {
            private bool buyed;
            private uint id;
            private DateTime lastDismount;

            public RhibBoat()
            {
                LastDismount = DateTime.MinValue;
            }

            public bool Buyed
            {
                get
                {
                    return buyed;
                }

                set
                {
                    buyed = value;
                }
            }

            public uint Id
            {
                get
                {
                    return id;
                }

                set
                {
                    id = value;
                }
            }

            public DateTime LastDismount
            {
                get
                {
                    return lastDismount;
                }

                set
                {
                    lastDismount = value;
                }
            }

            public void UpdateLastDismount(DateTime dateTime)
            {
                LastDismount = dateTime;
            }
        }
        #endregion
    }
}
