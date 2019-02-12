using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;


namespace Oxide.Plugins
{
    [Info("Vehicle Licence", "Sorrow", "1.1.0")]
    [Description("Allows players to buy vehicles and then spawn or store it")]

    class VehicleLicence : RustPlugin
    {
        #region Fields
        [PluginReference]
        Plugin Economics, ServerRewards;

        private ConfigData _configData;

        private Dictionary<ulong, LicencedPlayer> _licencedPlayer = new Dictionary<ulong, LicencedPlayer>();
        private Dictionary<uint, Vehicle> _vehiclesCache = new Dictionary<uint, Vehicle>();

        private int _intervalToCheckVehicle;
        private int _timeBeforeVehicleWipe;

        private bool _useEconomics;
        private bool _useServerRewards;
        private bool _usePermissions;
        private string _itemsNeededToBuyVehicles;

        const string prefix = "<color='orange'>[Licence]</color> ";
        const string rowBoatPrefab = "assets/content/vehicles/boats/rowboat/rowboat.prefab";
        const string rhibPrefab = "assets/content/vehicles/boats/rhib/rhib.prefab";
        const string sedanPrefab = "assets/content/vehicles/sedan_a/sedantest.entity.prefab";
        const string hotAirBalloonPrefab = "assets/prefabs/deployable/hot air balloon/hotairballoon.prefab";
        const string miniCopterPrefab = "assets/content/vehicles/minicopter/minicopter.entity.prefab";
        const string chinookPrefab = "assets/prefabs/npc/ch47/ch47.entity.prefab";
        #endregion

        #region uMod Hooks
        private void OnServerInitialized()
        {
            _intervalToCheckVehicle = _configData.Settings.IntervalToCheckVehicles;
            _timeBeforeVehicleWipe = _configData.Settings.TimeBeforeVehicleWipe;
            _useEconomics = _configData.Settings.UseEconomics;
            _useServerRewards = _configData.Settings.UseServerRewards;
            _itemsNeededToBuyVehicles = _configData.Settings.ItemsNeededToBuyVehicles;
            _usePermissions = _configData.Settings.UsePermissions;

            if (Economics == null && _useEconomics)
            {
                PrintWarning("Economics is not loaded, get it at https://umod.org");
            }
            if (ServerRewards == null && _useServerRewards)
            {
                PrintWarning("ServerRewards is not loaded, get it at https://umod.org");
            }

            LoadData();
            CheckVehicles();
            BroadcastHelp();
        }

        private void Loaded()
        {
            permission.RegisterPermission("vehiclelicence.use", this);
            permission.RegisterPermission("vehiclelicence.rowboat", this);
            permission.RegisterPermission("vehiclelicence.rhib", this);
            permission.RegisterPermission("vehiclelicence.sedan", this);
            permission.RegisterPermission("vehiclelicence.hotairballoon", this);
            permission.RegisterPermission("vehiclelicence.minicopter", this);
            permission.RegisterPermission("vehiclelicence.chinook", this);
        }

        private void Unload()
        {
            foreach (var vehicle in _vehiclesCache.ToList())
            {
                RemoveVehicle(GetLicencedPlayer(vehicle.Value), GetVehicleSettings(vehicle.Value.Prefab));
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
            Vehicle vehicle;
            if (!_vehiclesCache.TryGetValue(vehicleParent.net.ID, out vehicle)) return;
            vehicle.LastDismount = DateTime.UtcNow;
        }

        private void OnEntityKill(BaseNetworkable entity)
        {
            if (entity == null || entity.net?.ID == null) return;
            Vehicle vehicle;
            if (!_vehiclesCache.TryGetValue(entity.net.ID, out vehicle)) return;
            _vehiclesCache.Remove(entity.net.ID);
            vehicle.Id = 0;
        }
        #endregion

        #region Commands
        /// <summary>
        /// Commands the licence help.
        /// </summary>
        /// <param name="player">The player.</param>
        /// <param name="command">The command.</param>
        /// <param name="args">The arguments.</param>
        [ChatCommand("licence")]
        void CmdLicenceHelp(BasePlayer player, string command, string[] args)
        {
            SendReply(player, Msg("helpLicence", player.UserIDString));
            LicencedPlayer licencedPlayer;
            if (!_licencedPlayer.TryGetValue(player.userID, out licencedPlayer))
            {
                licencedPlayer = new LicencedPlayer(player.userID);
                _licencedPlayer.Add(player.userID, licencedPlayer);
            }
        }

        /// <summary>
        /// Commands the buy vehicle.
        /// </summary>
        /// <param name="player">The player.</param>
        /// <param name="command">The command.</param>
        /// <param name="args">The arguments.</param>
        [ChatCommand("buy")]
        void CmdBuyVehicle(BasePlayer player, string command, string[] args)
        {
            if (args.Length == 0) SendReply(player, string.Format(Msg("helpBuy", player.UserIDString), _itemsNeededToBuyVehicles,
                GetVehicleSettings(rowBoatPrefab).price, GetVehicleSettings(rhibPrefab).price, GetVehicleSettings(sedanPrefab).price,
                GetVehicleSettings(hotAirBalloonPrefab).price, GetVehicleSettings(miniCopterPrefab).price, GetVehicleSettings(chinookPrefab).price));
            if (args.Length >= 1)
            {
                LicencedPlayer licencedPlayer;
                if (!_licencedPlayer.TryGetValue(player.userID, out licencedPlayer))
                {
                    licencedPlayer = new LicencedPlayer(player.userID);
                    _licencedPlayer.Add(player.userID, licencedPlayer);
                }

                var arg = args[0].ToLower();
                if (!PlayerHasPermission(player, arg))
                {
                    SendReply(player, Msg("noPermission", player.UserIDString));
                    return;
                }
                if (IsCase(arg, rowBoatPrefab))
                {
                    BuyVehicle(player, licencedPlayer, rowBoatPrefab);
                }
                else if (IsCase(arg, rhibPrefab))
                {
                    BuyVehicle(player, licencedPlayer, rhibPrefab);
                }
                else if (IsCase(arg, sedanPrefab))
                {
                    BuyVehicle(player, licencedPlayer, sedanPrefab);
                }
                else if (IsCase(arg, hotAirBalloonPrefab))
                {
                    BuyVehicle(player, licencedPlayer, hotAirBalloonPrefab);
                }
                else if (IsCase(arg, miniCopterPrefab))
                {
                    BuyVehicle(player, licencedPlayer, miniCopterPrefab);
                }
                else if (IsCase(arg, chinookPrefab))
                {
                    BuyVehicle(player, licencedPlayer, chinookPrefab);
                }
                else
                {
                    SendReply(player, Msg("helpOptionNotFound", player.UserIDString));
                }
            }
        }

        /// <summary>
        /// Commands the spawn vehicle.
        /// </summary>
        /// <param name="player">The player.</param>
        /// <param name="command">The command.</param>
        /// <param name="args">The arguments.</param>
        [ChatCommand("spawn")]
        void CmdSpawnVehicle(BasePlayer player, string command, string[] args)
        {
            if (args.Length == 0) SendReply(player, Msg("helpSpawn", player.UserIDString));
            if (args.Length >= 1)
            {
                if (player.IsBuildingBlocked())
                {
                    SendReply(player, Msg("buildindBlocked", player.UserIDString));
                    return;
                }

                Vector3 position = player.transform.position + (player.transform.forward * 3);
                LicencedPlayer licencedPlayer;
                string prefab;
                if (_licencedPlayer.TryGetValue(player.userID, out licencedPlayer))
                {
                    var arg = args[0].ToLower();
                    if (!PlayerHasPermission(player, arg))
                    {
                        SendReply(player, Msg("noPermission", player.UserIDString));
                        return;
                    }
                    if (IsCase(arg, rowBoatPrefab))
                    {
                        prefab = rowBoatPrefab;
                        if (IsSpawning(licencedPlayer, prefab, true)) SpawnVehicle(licencedPlayer, prefab);
                    }
                    else if (IsCase(arg, rhibPrefab))
                    {
                        prefab = rhibPrefab;
                        if (IsSpawning(licencedPlayer, prefab, true)) SpawnVehicle(licencedPlayer, prefab);
                    }
                    else if (IsCase(arg, sedanPrefab))
                    {
                        prefab = sedanPrefab;
                        if (IsSpawning(licencedPlayer, prefab)) SpawnVehicle(licencedPlayer, prefab);
                    }
                    else if (IsCase(arg, hotAirBalloonPrefab))
                    {
                        prefab = hotAirBalloonPrefab;
                        if (IsSpawning(licencedPlayer, prefab)) SpawnVehicle(licencedPlayer, prefab);
                    }
                    else if (IsCase(arg, miniCopterPrefab))
                    {
                        prefab = miniCopterPrefab;
                        if (IsSpawning(licencedPlayer, prefab)) SpawnVehicle(licencedPlayer, prefab);
                    }
                    else if (IsCase(arg, chinookPrefab))
                    {
                        prefab = chinookPrefab;
                        if (IsSpawning(licencedPlayer, prefab)) SpawnVehicle(licencedPlayer, prefab);
                    }
                    else
                    {
                        SendReply(player, Msg("helpOptionNotFound", player.UserIDString));
                    }
                } else
                {
                    SendReply(player, Msg("didntBuyVehicle", player.UserIDString));
                }
            }
        }

        /// <summary>
        /// Commands the recall vehicle.
        /// </summary>
        /// <param name="player">The player.</param>
        /// <param name="command">The command.</param>
        /// <param name="args">The arguments.</param>
        [ChatCommand("recall")]
        void CmdRecallVehicle(BasePlayer player, string command, string[] args)
        {
            LicencedPlayer licencedPlayer;

            if (args.Length == 0) SendReply(player, Msg("helpRecall", player.UserIDString));
            if (args.Length >= 1)
            {
                if (_licencedPlayer.TryGetValue(player.userID, out licencedPlayer)) {
                    var arg = args[0].ToLower();
                    if (!PlayerHasPermission(player, arg))
                    {
                        SendReply(player, Msg("noPermission", player.UserIDString));
                        return;
                    }
                    if (IsCase(arg, rowBoatPrefab))
                    {
                        RemoveVehicle(licencedPlayer, GetVehicleSettings(rowBoatPrefab));
                    }
                    else if (IsCase(arg, rhibPrefab))
                    {
                        RemoveVehicle(licencedPlayer, GetVehicleSettings(rhibPrefab));
                    }
                    else if (IsCase(arg, sedanPrefab))
                    {
                        RemoveVehicle(licencedPlayer, GetVehicleSettings(sedanPrefab));
                    }
                    else if (IsCase(arg, hotAirBalloonPrefab))
                    {
                        RemoveVehicle(licencedPlayer, GetVehicleSettings(hotAirBalloonPrefab));
                    }
                    else if (IsCase(arg, miniCopterPrefab))
                    {
                        RemoveVehicle(licencedPlayer, GetVehicleSettings(miniCopterPrefab));
                    }
                    else if (IsCase(arg, chinookPrefab))
                    {
                        RemoveVehicle(licencedPlayer, GetVehicleSettings(chinookPrefab));
                    }
                    else
                    {
                        SendReply(player, Msg("helpOptionNotFound", player.UserIDString));
                    }
                }
            }
        }


        #endregion

        #region Functions
        /// <summary>
        /// Buys the vehicle.
        /// </summary>
        /// <param name="player">The player.</param>
        /// <param name="licencedPlayer">The licenced player.</param>
        /// <param name="prefab">The prefab.</param>
        private void BuyVehicle(BasePlayer player, LicencedPlayer licencedPlayer, string prefab)
        {
            Vehicle vehicle;
            var vehicleSettings = GetVehicleSettings(prefab);

            if (licencedPlayer.Vehicles.TryGetValue(prefab, out vehicle))
            {
                SendReply(player, string.Format(Msg("vehicleAlreadyPurchased", player.UserIDString), vehicleSettings.name));
            }
            else if (vehicleSettings.name != "null" && vehicleSettings.purchasable)
            {
                if (Withdraw(player, vehicleSettings))
                {
                    vehicle = new Vehicle(prefab, player.userID);
                    licencedPlayer.SetVehicle(vehicle);
                }
            }
            else
            {
                SendReply(player, string.Format(Msg("vehicleCannotBeBuyed", player.UserIDString), vehicleSettings.name));
            }
        }

        /// <summary>
        /// Spawns the vehicle.
        /// </summary>
        /// <param name="licencedPlayer">The licenced player.</param>
        /// <param name="prefab">The prefab.</param>
        /// <returns></returns>
        private BaseEntity SpawnVehicle(LicencedPlayer licencedPlayer, string prefab)
        {
            var player = licencedPlayer.Player;
            if (player == null) return null;
            var vehicleSettings = GetVehicleSettings(prefab);
            var vehicle = licencedPlayer.GetVehicle(prefab);
            if (vehicle == null) return null;
            var position = player.transform.position + new Vector3(0f, 1.6f, 0f);
            var rotation = player.transform.rotation;
            BaseEntity entity = GameManager.server.CreateEntity(vehicle.Prefab, position + (Vector3.forward * vehicleSettings.distanceToSpawn), rotation);
            if (entity == null) return null;
            entity.enableSaving = true;
            entity.Spawn();
            if (entity.net == null) return null;
            vehicle.Spawned = (DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0));
            vehicle.Id = entity.net.ID;
            vehicle.LastDismount = DateTime.UtcNow;
            _vehiclesCache.Add(vehicle.Id, vehicle);
            licencedPlayer.SetVehicle(vehicle);
            SendReply(player, string.Format(Msg("vehicleSpawned", player.UserIDString), vehicleSettings.name));

            return entity;
        }

        /// <summary>
        /// Removes the vehicle.
        /// </summary>
        /// <param name="licencedPlayer">The licenced player.</param>
        /// <param name="vehicleSettings">The vehicle settings.</param>
        private void RemoveVehicle(LicencedPlayer licencedPlayer, VehicleSettings vehicleSettings)
        {
            var player = licencedPlayer.Player;
            var vehicle = licencedPlayer.GetVehicle(vehicleSettings.prefab);
            if (player != null && vehicle == null)
            {
                SendReply(player, string.Format(Msg("vehicleNotYetPurchased", player.UserIDString), vehicleSettings.name));
            } else
            {
                var vehicleId = vehicle.Id;
                _vehiclesCache.Remove(vehicle.Id);
                BaseNetworkable.serverEntities.Find(vehicle.Id)?.Kill();
                vehicle.Id = 0;
                licencedPlayer.SetVehicle(vehicle);
                if (player != null && vehicleId != 0)
                {
                    SendReply(player, string.Format(Msg("vehicleRecalled", player.UserIDString), vehicleSettings.name));
                }
                else if (player != null && vehicleId == 0)
                {
                    SendReply(player, string.Format(Msg("vehicleNotOut", player.UserIDString), vehicleSettings.name));
                }
            }
        }

        /// <summary>
        /// Checks the vehicles.
        /// </summary>
        private void CheckVehicles()
        {
            foreach (var v in _vehiclesCache.ToList())
            {
                var vehicle = v.Value;
                var vehicleSettings = GetVehicleSettings(vehicle.Prefab);
                var vehicleNetworkable = BaseNetworkable.serverEntities.Find(vehicle.Id);
                if (vehicleNetworkable == null) continue;
                var vehicleEntity = vehicleNetworkable.GetComponent<BaseVehicle>();
                if (vehicleEntity == null) continue;
                if (vehicleEntity.IsMounted()) continue;
                if (VehicleIsActive(vehicle)) continue;
                RemoveVehicle(GetLicencedPlayer(vehicle), GetVehicleSettings(vehicle.Prefab));
            }

            timer.Once(_intervalToCheckVehicle * 60f, () => CheckVehicles());
        }

        /// <summary>
        /// Broadcasts the help.
        /// </summary>
        private void BroadcastHelp()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                SendReply(player, Msg("announcement", player.UserIDString));
            }

            timer.Once(60 * UnityEngine.Random.Range(15, 45), () => CheckVehicles());
        }
        #endregion

        #region Helpers
        /// <summary>
        /// Determines whether the specified argument is case.
        /// </summary>
        /// <param name="arg">The argument.</param>
        /// <param name="prefab">The prefab.</param>
        /// <returns>
        ///   <c>true</c> if the specified argument is case; otherwise, <c>false</c>.
        /// </returns>
        private bool IsCase(string arg, string prefab)
        {
            return GetVehicleSettings(prefab).commands.IndexOf(arg) >= 0 && GetVehicleSettings(prefab).commands.IndexOf(arg) < GetVehicleSettings(prefab).commands.Count;
        }

        /// <summary>
        /// Withdraws the specified player.
        /// </summary>
        /// <param name="player">The player.</param>
        /// <param name="vehicleSettings">The vehicle settings.</param>
        /// <returns></returns>
        private bool Withdraw(BasePlayer player, VehicleSettings vehicleSettings)
        {
            bool result = false;
            var item = ItemManager.FindItemDefinition(_itemsNeededToBuyVehicles);

            if (Economics != null && _useEconomics)
            {
                result = Economics.Call<bool>("Withdraw", player.userID, Convert.ToDouble(vehicleSettings.price));
            }
            else if (ServerRewards != null && _useServerRewards)
            {
                result = ServerRewards.Call<bool>("TakePoints", player.userID, Convert.ToDouble(vehicleSettings.price));
            }
            else if (item != null && player.inventory.GetAmount(item.itemid) >= vehicleSettings.price)
            {
                player.inventory.Take(null, item.itemid, vehicleSettings.price);
                result = true;
            }

            if (result)
            {
                SendReply(player, string.Format(Msg("vehiclePurchased", player.UserIDString), vehicleSettings.name));
                return true;
            }
            else
            {
                SendReply(player, Msg("noMoney", player.UserIDString));
                return false;
            }
        }

        /// <summary>
        /// Determines whether the specified licenced player is spawning.
        /// </summary>
        /// <param name="licencedPlayer">The licenced player.</param>
        /// <param name="prefab">The prefab.</param>
        /// <param name="water">if set to <c>true</c> [water].</param>
        /// <returns>
        ///   <c>true</c> if the specified licenced player is spawning; otherwise, <c>false</c>.
        /// </returns>
        private bool IsSpawning(LicencedPlayer licencedPlayer, string prefab, bool water = false)
        {
            var player = licencedPlayer.Player;
            if (player == null) return false;
            var vehicleSettings = GetVehicleSettings(prefab);
            var vehicle = licencedPlayer.GetVehicle(prefab);
            if (vehicle == null)
            {
                SendReply(player, string.Format(Msg("vehicleNotYetPurchased", player.UserIDString), vehicleSettings.name));
                return false;
            }
            else if (vehicle.Id != 0)
            {
                SendReply(player, string.Format(Msg("alreadyVehicleOut", player.UserIDString), vehicleSettings.name));
                return false;
            }
            else if (water && !IsInWater(player))
            {
                SendReply(player, Msg("notInWater", player.UserIDString));
                return false;
            }
            else if (vehicleSettings.cooldownToSpawn > 0 && vehicle.Spawned > (DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0)).Subtract(TimeSpan.FromSeconds(vehicleSettings.cooldownToSpawn)))
            {
                SendReply(player, string.Format(Msg("vehicleOnCooldown", player.UserIDString),
                    Convert.ToInt32((vehicle.Spawned - (DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0)).Subtract(TimeSpan.FromSeconds(vehicleSettings.cooldownToSpawn))).TotalSeconds),
                    vehicleSettings.name));
                return false;
            } else
            {
                return true;
            }
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

        /// <summary>
        /// Player has permission.
        /// </summary>
        /// <param name="player">The player.</param>
        /// <param name="arg">The argument.</param>
        /// <returns></returns>
        private bool PlayerHasPermission(BasePlayer player, string arg)
        {
            if (!_usePermissions) return true;
            if (permission.UserHasPermission(player.UserIDString, "vehiclelicence.use")) return true;
            if (IsCase(arg, rowBoatPrefab))
            {
                return permission.UserHasPermission(player.UserIDString, "vehiclelicence.rowboat") ? true : false;
            }
            else if (IsCase(arg, rhibPrefab))
            {
                return permission.UserHasPermission(player.UserIDString, "vehiclelicence.rhib") ? true : false;
            }
            else if (IsCase(arg, sedanPrefab))
            {
                return permission.UserHasPermission(player.UserIDString, "vehiclelicence.sedan") ? true : false;
            }
            else if (IsCase(arg, hotAirBalloonPrefab))
            {
                return permission.UserHasPermission(player.UserIDString, "vehiclelicence.hotairballoon") ? true : false;
            }
            else if (IsCase(arg, miniCopterPrefab))
            {
                return permission.UserHasPermission(player.UserIDString, "vehiclelicence.minicopter") ? true : false;
            }
            else if (IsCase(arg, chinookPrefab))
            {
                return permission.UserHasPermission(player.UserIDString, "vehiclelicence.chinook") ? true : false;
            }
            return false;
        }

        /// <summary>
        /// Gets the vehicle settings.
        /// </summary>
        /// <param name="prefab">The prefab.</param>
        /// <returns></returns>
        private VehicleSettings GetVehicleSettings(string prefab)
        {
            switch (prefab)
            {
                case rowBoatPrefab:
                    return _configData.Vehicles.RowBoat;
                case rhibPrefab:
                    return _configData.Vehicles.RHIB;
                case sedanPrefab:
                    return _configData.Vehicles.Sedan;
                case hotAirBalloonPrefab:
                    return _configData.Vehicles.HotAirBalloon;
                case miniCopterPrefab:
                    return _configData.Vehicles.MiniCopter;
                case chinookPrefab:
                    return _configData.Vehicles.Chinook;
                default:
                    return new VehicleSettings("null", "null", false, 999999, -1, 0, new List<string>());
            }
        }

        /// <summary>
        /// Gets the licenced player.
        /// </summary>
        /// <param name="vehicle">The vehicle.</param>
        /// <returns></returns>
        private LicencedPlayer GetLicencedPlayer(Vehicle vehicle)
        {
            LicencedPlayer licencedPlayer;
            if (_licencedPlayer.TryGetValue(vehicle.Player.userID, out licencedPlayer)) return licencedPlayer;
            return null;
        }

        /// <summary>
        /// Vehicles the is active.
        /// </summary>
        /// <param name="vehicle">The vehicle.</param>
        /// <returns></returns>
        private bool VehicleIsActive(Vehicle vehicle)
        {
            return vehicle.LastDismount.Ticks >= DateTime.UtcNow.Subtract(TimeSpan.FromMinutes(_timeBeforeVehicleWipe)).Ticks;
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
                ["announcement"] = "Type <color='green'>/licence</color> to get help.",
                ["helpLicence"] = "These are the available commands: \n" +
                    "<color='green'>/buy</color> -- To buy a vehicle \n" +
                    "<color='green'>/spawn</color> -- To spawn a vehicle \n" +
                    "<color='green'>/recall</color> -- To recall a vehicle",
                ["helpBuy"] = "These are the available commands: \n" +
                    "Money: <color='red'>{0}</color> \n" +
                    "<color='green'>/buy row</color> -- <color='red'>{1}</color> to buy a rowing boat \n" +
                    "<color='green'>/buy rhib</color> -- <color='red'>{2}</color> to buy a RHIB \n" +
                    "<color='green'>/buy sedan</color> -- <color='red'>{3}</color> to buy a sedan \n" +
                    "<color='green'>/buy hab</color> -- <color='red'>{4}</color> to buy an hot air balloon \n" +
                    "<color='green'>/buy copter</color> -- <color='red'>{5}</color> to buy a mini copter \n" +
                    "<color='green'>/buy ch47</color> -- <color='red'>{6}</color> to buy a chinook \n",
                ["helpSpawn"] = "These are the available commands: \n" +
                    "<color='green'>/spawn row</color> -- To spawn a rowing boat \n" +
                    "<color='green'>/spawn rhib</color> -- To spawn a RHIB \n" +
                    "<color='green'>/spawn sedan</color> -- To spawn a sedan \n" +
                    "<color='green'>/spawn hab</color> -- To spawn an hot air balloon \n" +
                    "<color='green'>/spawn copter</color> -- To spawn a mini copter \n" +
                    "<color='green'>/spawn ch47</color> -- To spawn a chinook \n",
                ["helpRecall"] = "These are the available commands: \n" +
                    "<color='green'>/recall row</color> -- To recall a rowing boat \n" +
                    "<color='green'>/recall rhib</color> -- To recall a RHIB \n" +
                    "<color='green'>/recall sedan</color> -- To recall a sedan \n" +
                    "<color='green'>/recall hab</color> -- To recall an hot air balloon \n" +
                    "<color='green'>/recall copter</color> -- To recall a mini copter \n" +
                    "<color='green'>/recall ch47</color> -- To recall a Chinook \n",
                ["helpOptionNotFound"] = "This option doesn't exist.",
                ["vehiclePurchased"] = "You have purchased a {0}, type <color='green'>/spawn</color> for more information.",
                ["vehicleAlreadyPurchased"] = "You have already purchased {0}.",
                ["vehicleCannotBeBuyed"] = "You can't buy a {0}.",
                ["vehicleNotOut"] = "{0} is not out.",
                ["noMoney"] = "You don't have enough money.",
                ["didntBuyVehicle"] = "You didn't purchase a vehicle.",
                ["alreadyVehicleOut"] = "You already have a {0} outside, type <color='green'>/spawn</color> for more information.",
                ["vehicleNotYetPurchased"] = "You have not yet purchased a {0}.",
                ["vehicleSpawned"] = "You spawned your {0}.",
                ["vehicleRecalled"] = "You recalled your {0}.",
                ["vehicleOnCooldown"] = "You must wait {0} seconds before you can spawn your {1}.",
                ["notInWater"] = "You must be in the water to use this command.",
                ["buildindBlocked"] = " You can't spawn a boat appear if you don't have the building privileges.",
                ["noPermission"] = "You do not have permission to do this.",
            }, this);

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["announcement"] = "Tapez <color='green'>/licence</color> pour obtenir de l'aide.",
                ["helpLicence"] = "Voici les commandes disponibles : \n" +
                    "<color='green'>/buy</color> -- Pour acheter un véhicule \n" +
                    "<color='green'>/spawn</color> -- Pour faire apparaître un véhicule \n" +
                    "<color='green'>/recall</color> -- Pour ranger un véhicule",
                ["helpBuy"] = "Voici les commandes disponibles : \n" +
                    "Argent : <color='red'>{0}</color> \n" +
                    "<color='green'>/buy row</color> -- <color='red'>{1}</color> pour acheter un bateau à rames \n" +
                    "<color='green'>/buy rhib</color> -- <color='red'>{2}</color> pour acheter un RHIB \n" +
                    "<color='green'>/buy sedan</color> -- <color='red'>{3}</color> pour acheter une voiture \n" +
                    "<color='green'>/buy hab</color> -- <color='red'>{4}</color> pour acheter une montgolfière \n" +
                    "<color='green'>/buy copter</color> -- <color='red'>{5}</color> pour acheter un mini hélicoptère \n" +
                    "<color='green'>/buy ch47</color> -- <color='red'>{6}</color> pour acheter un Chinook \n",
                ["helpSpawn"] = "Voici les commandes disponibles : \n" +
                    "<color='green'>/spawn row</color> -- Pour faire apparaître un bateau à rames \n" +
                    "<color='green'>/spawn rhib</color> -- Pour faire apparaître un RHIB \n" +
                    "<color='green'>/spawn sedan</color> -- Pour faire apparaître une voiture \n" +
                    "<color='green'>/spawn hab</color> -- Pour faire apparaître une montgolfière \n" +
                    "<color='green'>/spawn copter</color> -- Pour faire apparaître un mini hélicoptère \n" +
                    "<color='green'>/spawn ch47</color> -- Pour faire apparaître un Chinook \n",
                ["helpRecall"] = "Voici les commandes disponibles : \n" +
                    "<color='green'>/recall row</color> -- Pour ranger un bateau à rames \n" +
                    "<color='green'>/recall rhib</color> -- Pour ranger un RHIB \n" +
                    "<color='green'>/recall sedan</color> -- Pour ranger une voiture \n" +
                    "<color='green'>/recall hab</color> -- Pour ranger une montgolfière \n" +
                    "<color='green'>/recall copter</color> -- Pour ranger un mini hélicoptère \n" +
                    "<color='green'>/recall ch47</color> -- Pour ranger un Chinook \n",
                ["helpOptionNotFound"] = "Cette option n'existe pas.",
                ["vehiclePurchased"] = "Vous avez acheté un {0}, tapez <color='green'>/spawn</color> pour plus d'informations.",
                ["vehicleAlreadyPurchased"] = "Vous avez déjà acheté ce {0}.",
                ["vehicleCannotBeBuyed"] = "Vous ne pouvez pas acheter un {0}.",
                ["vehicleNotOut"] = "{0} n'est pas dehors.",
                ["noMoney"] = "Vous n'avez pas assez d'argent.",
                ["didntBuyVehicle"] = "Vous n'avez pas acheté de {0}.",
                ["alreadyVehicleOut"] = "Vous avez déjà un {0} à l'extérieur, tapez <color='green'>/recall</color> pour plus d'informations.",
                ["vehicleNotYetPurchased"] = "Vous n'avez pas encore acheté de {0}.",
                ["vehicleSpawned"] = "Vous avez fait apparaître votre {0}.",
                ["vehicleRecalled"] = "Vous avez rangé votre {0}.",
                ["vehicleOnCooldown"] = "Vous devez attendre {0} secondes avant de pouvoir faire apparaître votre {1}.",
                ["notInWater"] = "Vous devez être dans l'eau pour utiliser cette commande.",
                ["buildindBlocked"] = "Vous ne pouvez pas faire apparaître un {0} si vous n'avez pas les privilèges de construction.",
                ["noPermission"] = "Vous n'avez pas la permission de faire ceci.",
            }, this, "fr");
        }
        #endregion

        #region Config
        /// <summary>
        /// Loads the configuration.
        /// </summary>
        protected override void LoadConfig()
        {
            base.LoadConfig();
            _configData = Config.ReadObject<ConfigData>();

            Config.WriteObject(_configData, true);
        }

        /// <summary>
        /// Loads the default configuration.
        /// </summary>
        protected override void LoadDefaultConfig() => _configData = GetBaseConfig();

        /// <summary>
        /// Saves the configuration.
        /// </summary>
        protected override void SaveConfig() => Config.WriteObject(_configData, true);

        /// <summary>
        /// Loads the data.
        /// </summary>
        private void LoadData()
        {
            _licencedPlayer = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, LicencedPlayer>>("VehicleLicence");
        }

        /// <summary>
        /// Saves the data.
        /// </summary>
        private void SaveData() => Interface.Oxide.DataFileSystem.WriteObject("VehicleLicence", _licencedPlayer);

        /// <summary>
        /// Gets the base configuration.
        /// </summary>
        /// <returns>
        /// Config data
        /// </returns>
        private ConfigData GetBaseConfig()
        {
            return new ConfigData
            {
                Settings = new ConfigData.SettingsOption
                {
                    IntervalToCheckVehicles = 5,
                    TimeBeforeVehicleWipe = 15,
                    UseEconomics = false,
                    UseServerRewards = false,
                    ItemsNeededToBuyVehicles = "scrap",
                    UsePermissions = false,
                },

                Vehicles = new ConfigData.VehiclesOption
                {
                    RowBoat = new VehicleSettings("Row Boat", rowBoatPrefab, true, 500, 180, 3, new List<string>() { "row", "rowboat" }),
                    RHIB = new VehicleSettings("RHIB", rhibPrefab, true, 1000, 300, 10, new List<string>() { "rhib" }),
                    Sedan = new VehicleSettings("Sedan", sedanPrefab, true, 300, 180, 5, new List<string>() { "sedan", "car" }),
                    HotAirBalloon = new VehicleSettings("Hot Air Balloon", hotAirBalloonPrefab, true, 5000, 900, 20, new List<string>() { "hotairballoon", "hab" }),
                    MiniCopter = new VehicleSettings("MiniCopter", miniCopterPrefab, true, 10000, 1800, 8, new List<string>() { "minicopter", "copter" }),
                    Chinook = new VehicleSettings("Chinook", chinookPrefab, true, 30000, 3000, 25, new List<string>() { "chinook", "ch47" }),
                }
            };
        }
        #endregion

        #region Class
        class ConfigData
        {
            [JsonProperty(PropertyName = "Define your licence settings")]
            public SettingsOption Settings { get; set; }

            public class SettingsOption
            {
                [JsonProperty(PropertyName = "Interval in minutes to check vehicle for wipe")]
                public int IntervalToCheckVehicles { get; set; }
                [JsonProperty(PropertyName = "Time before vehicle wipe in minutes")]
                public int TimeBeforeVehicleWipe { get; set; }
                [JsonProperty(PropertyName = "Use Economics to buy vehicles")]
                public bool UseEconomics { get; set; }
                [JsonProperty(PropertyName = "Use ServerRewars to buy vehicles")]
                public bool UseServerRewards { get; set; }
                [JsonProperty(PropertyName = "Shortname of item needed to buy vehicles")]
                public string ItemsNeededToBuyVehicles { get; set; }
                [JsonProperty(PropertyName = "Use permissions for chat commands")]
                public bool UsePermissions { get; set; }
            }

            [JsonProperty(PropertyName = "Define your vehicles options")]
            public VehiclesOption Vehicles { get; set; }

            public class VehiclesOption
            {
                [JsonProperty(PropertyName = "RowBoat")]
                public VehicleSettings RowBoat { get; set; }
                [JsonProperty(PropertyName = "RHIB")]
                public VehicleSettings RHIB { get; set; }
                [JsonProperty(PropertyName = "Sedan")]
                public VehicleSettings Sedan { get; set; }
                [JsonProperty(PropertyName = "HotAirBalloon")]
                public VehicleSettings HotAirBalloon { get; set; }
                [JsonProperty(PropertyName = "MiniCopter")]
                public VehicleSettings MiniCopter { get; set; }
                [JsonProperty(PropertyName = "Chinook")]
                public VehicleSettings Chinook { get; set; }
            }
        }

        class LicencedPlayer
        {
            public readonly ulong Userid;
            public Dictionary<string, Vehicle> Vehicles;

            [JsonConstructor]
            public LicencedPlayer(ulong userid)
            {
                Userid = userid;
                Vehicles = new Dictionary<string, Vehicle>();
            }

            [JsonIgnore]
            public BasePlayer Player => BasePlayer.FindByID(Userid);

            [JsonIgnore]
            public ulong userid
            {
                get
                {
                    return Userid;
                }
            }

            public void SetVehicle(Vehicle vehicle)
            {
                if (Vehicles.ContainsKey(vehicle.Prefab))
                {
                    Vehicles[vehicle.Prefab] = vehicle;
                }
                else
                {
                    Vehicles.Add(vehicle.Prefab, vehicle);
                }
            }

            public Vehicle GetVehicle(string prefab)
            {
                Vehicle result = null;

                if (Vehicles.ContainsKey(prefab))
                {
                    result = Vehicles[prefab];
                }

                return result;
            }

            public Vehicle GetVehicle(Vehicle vehicle)
            {
                Vehicle result = null;

                if (Vehicles.ContainsKey(vehicle.Prefab))
                {
                    result = Vehicles[vehicle.Prefab];
                }

                return result;
            }
        }

        class Vehicle
        {
            public ulong Userid { get; }
            public string Prefab { get; }
            public uint Id { get; set;  }
            public TimeSpan Spawned { get; set; }
            public DateTime LastDismount { get; set; }

            [JsonConstructor]
            public Vehicle(string prefab, ulong userid)
            {
                Userid = userid;
                Prefab = prefab;
                LastDismount = DateTime.MinValue;
            }

            [JsonIgnore]
            public BasePlayer Player => BasePlayer.FindByID(Userid);
        }

        class VehicleSettings
        {
            private string Name;
            private string Prefab;
            private bool Purchasable;
            private int Price;
            private int CooldownToSpawn;
            private int DistanceToSpawn;
            private List<string> Commands;

            [JsonConstructor]
            public VehicleSettings(string name, string prefab, bool purchasable, int price, int cooldownToSpawn, int distanceToSpawn, List<string> commands)
            {
                Name = name;
                Prefab = prefab;
                Purchasable = purchasable;
                Price = price;
                CooldownToSpawn = cooldownToSpawn;
                DistanceToSpawn = distanceToSpawn;
                Commands = commands;
            }

            public string name
            {
                get
                {
                    return Name;
                }
            }

            public string prefab
            {
                get
                {
                    return Prefab;
                }
            }

            public bool purchasable
            {
                get
                {
                    return Purchasable;
                }
            }

            public int price
            {
                get
                {
                    return Price;
                }
            }

            public int cooldownToSpawn
            {
                get
                {
                    return CooldownToSpawn;
                }
            }

            public int distanceToSpawn
            {
                get
                {
                    return DistanceToSpawn;
                }
            }

            public List<string> commands
            {
                get
                {
                    return Commands;
                }
            }
        }
        #endregion
    }
}
