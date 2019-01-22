using System;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("Inbound", "Tori1157", "0.4.5")]
    [Description("Notifies all players when a attack helicopter, cargo plane, bradley, cargo-ship or supply drop is inbound.")]

    class Inbound : RustPlugin
    {
        #region Initialization

        bool cargoPlaneAlerts;
        bool helicopterAlerts;
        bool showCoordinates;
        bool supplyDropAlerts;
        bool cargoshipAlerts;
        bool chinookAlerts;
        bool playerSupplydropAlert;
        bool ApcBradleyAlerts;
        bool HackableAlerts;
        bool HackablePlayersAlert;

        protected override void LoadDefaultConfig()
        {
            // Options
            Config["Cargo Plane Alerts (true/false)"] = cargoPlaneAlerts = GetConfig("Cargo Plane Alerts (true/false)", true);
            Config["Helicopter Alerts (true/false)"] = helicopterAlerts = GetConfig("Helicopter Alerts (true/false)", true);
            Config["Show Coordinates (true/false)"] = showCoordinates = GetConfig("Show Coordinates (true/false)", true);
            Config["Supply Drop Alerts (true/false)"] = supplyDropAlerts = GetConfig("Supply Drop Alerts (true/false)", true);
            Config["Cargoship Alerts (true/false)"] = cargoshipAlerts = GetConfig("Cargoship Alerts (true/false)", true);
            Config["CH47 Chinook Alerts (true/false)"] = chinookAlerts = GetConfig("CH47 Chinook Alerts (true/false)", true);
            Config["Player Supply Drop Alert (true/false)"] = playerSupplydropAlert = GetConfig("Player Supply Drop Alert (true/false)", true);
            Config["APC Bradley Alerts (true/false)"] = ApcBradleyAlerts = GetConfig("APC Bradley Alerts (true/false)", true);
            Config["Hackable Crate Alerts (true/false)"] = HackableAlerts = GetConfig("Hackable Crate Alerts (true/false)", true);
            Config["Hackable Player In Alert (true/false)"] = HackablePlayersAlert = GetConfig("Hackable Player In Alert (true/false)", true);

            // Cleanup
            Config.Remove("Show Coordonates (true/false)");
            Config.Remove("HelicopterAlerts");
            Config.Remove("CargoshipAlerts");
            Config.Remove("SupplyDropAlerts");
            Config.Remove("ChinookAlerts");
            Config.Remove("PlayerSupplyDropAlert");
            Config.Remove("ApcBradleyAlerts");
            Config.Remove("HackableAlerts");
            Config.Remove("HackablePlayersAlert");

            SaveConfig();
        }

        void Init()
        {
            LoadDefaultConfig();
            LoadDefaultMessages();
        }

        #endregion

        #region Localization

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CargoPlaneInbound1"] = "Cargo plane inbound{0}!",
                ["HelicopterInbound1"] = "Helicopter inbound{0}!",
                ["SupplyDropInbound1"] = "Supply drop inbound{0}!",
                ["CargoshipInbound1"] = "Cargoship inbound{0}!",
                ["ChinookInbound1"] = "Chinook inbound{0}!",
                ["BradleyInbound"] = "Bradley inbound{0}!",
                ["PlayerSupplyDrop1"] = "{0} has deployed a supply drop{1}!",
                ["PositionAt"] = " at {0}",
                ["HackableInbound"] = "Hackable Crate dropped{0}!",
                ["HackablePlayer"] = "{0} has started hacking a crate{1}!",
            }, this);
        }

        #endregion

        #region Entity Checks

        void OnExplosiveThrown(BasePlayer player, BaseEntity entity)
        {
            if (!(entity is SupplySignal))
                return;

            var pos = showCoordinates ? $"{entity.transform.position}" : string.Empty;
            if (playerSupplydropAlert)
                Broadcast(Lang("PlayerSupplyDrop1", player.UserIDString, player.displayName, Position(pos)));
        }

        void OnExplosiveDropped(BasePlayer player, BaseEntity entity)
        {
            if (!(entity is SupplySignal))
                return;

            var pos = showCoordinates ? $"{entity.transform.position}" : string.Empty;
            if (playerSupplydropAlert)
                Broadcast(Lang("PlayerSupplyDrop1", player.UserIDString, player.displayName, Position(pos)));
        }

        void OnEntitySpawned(BaseEntity entity)
        {
            if (!(entity is CargoPlane) && !(entity is BaseHelicopter) && !(entity is SupplyDrop) && !(entity is CargoShip) && !(entity is CH47Helicopter) && !(entity is BradleyAPC) && !(entity is HackableLockedCrate)) return;

            var pos = showCoordinates ? $"{entity.transform.position.x}, {entity.transform.position.y}, {entity.transform.position.z}" : string.Empty;
            if (cargoPlaneAlerts && entity is CargoPlane) Broadcast(Lang("CargoPlaneInbound1", null, Position(pos)));
            if (helicopterAlerts && entity is BaseHelicopter) Broadcast(Lang("HelicopterInbound1", null, Position(pos)));
            if (supplyDropAlerts && entity is SupplyDrop) { Broadcast(Lang("SupplyDropInbound1", null, Position(pos))); }
            if (cargoshipAlerts && entity is CargoShip) { Broadcast(Lang("CargoshipInbound1", null, Position(pos))); }
            if (ApcBradleyAlerts && entity is BradleyAPC) { Broadcast(Lang("BradleyInbound", null, Position(pos))); }
            if (HackableAlerts && entity is HackableLockedCrate) { Broadcast(Lang("HackableInbound", null, Position(pos))); }
            if (chinookAlerts && entity is CH47HelicopterAIController)
            {
                var heli = entity as CH47HelicopterAIController;
                timer.Once(1f, () =>{ Broadcast(Lang("ChinookInbound1", null, Position((heli != null && showCoordinates ? $"{heli.GetMoveTarget().ToString()}" : string.Empty)))); });
            }
        }

        private object CanHackCrate(BasePlayer player, HackableLockedCrate crate)
        {
            if (player == null) return null;

            var pos = showCoordinates ? $"{crate.transform.position.x}, {crate.transform.position.y}, {crate.transform.position.z}" : string.Empty;

            if (HackablePlayersAlert) Broadcast(Lang("HackablePlayer", player.UserIDString, player.displayName, Position(pos)));

            return null;
        }

        #endregion

        #region Helpers

        T GetConfig<T>(string name, T value) => Config[name] == null ? value : (T)Convert.ChangeType(Config[name], typeof(T));

        string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);
        private string Position(string pos) => pos != string.Empty ? Lang("PositionAt", null, pos.Replace("(", string.Empty).Replace(")", string.Empty)) : string.Empty;

        void Broadcast(string message)
        {
            foreach (var player in BasePlayer.activePlayerList)
                PrintToChat(player, message);
        }

        #endregion
    }
}