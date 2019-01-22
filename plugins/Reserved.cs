using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("Reserved", "Wulf/lukespragg", "2.0.3")]
    [Description("Allows players with permission to always be able to connect")]
    public class Reserved : CovalencePlugin
    {
        #region Configuration

        private Configuration config;

        public class Configuration
        {
            [JsonProperty(PropertyName = "Always allow admin to connect (true/false)")]
            public bool AlwaysAllowAdmin { get; set; } = false;

            [JsonProperty(PropertyName = "Always use reserved slot if player has permission (true/false)")]
            public bool AlwaysUseSlot { get; set; } = false;

            [JsonProperty(PropertyName = "Dynamic slots based on players with permission (true/false)")]
            public bool DynamicSlots { get; set; } = false;

            [JsonProperty(PropertyName = "Kick other players for players with permission (true/false)")]
            public bool KickForReserved { get; set; } = true;

            [JsonProperty(PropertyName = "Number of slots to reserve (if dynamic slots not enabled)")]
            public int ReservedSlots { get; set; } = 5;
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null)
                {
                    LoadDefaultConfig();
                }
            }
            catch
            {
                LoadDefaultConfig();
            }
            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            string configPath = $"{Interface.Oxide.ConfigDirectory}{Path.DirectorySeparatorChar}{Name}.json";
            PrintWarning($"Could not load a valid configuration file, creating a new configuration file at {configPath}");
            config = new Configuration();
        }

        protected override void SaveConfig() => Config.WriteObject(config);

        #endregion Configuration

        #region Localization

        private new void LoadDefaultMessages()
        {
            // English
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["KickedForReserved"] = "Kicked for player with reserved slot",
                ["ReservedSlotsOnly"] = "Only reserved slots available",
                ["SlotsNowAvailable"] = "{0} slot(s) now available"
            }, this);
        }

        #endregion Localization

        #region Initialization

        private const string permSlot = "reserved.slot";

        private int slotsAvailable;

        private void OnServerInitialized()
        {
            permission.RegisterPermission(permSlot, this);

            slotsAvailable = Math.Min(config.ReservedSlots, server.MaxPlayers);
            int slotCount = players.All.Count(player => player.HasPermission(permSlot));

            if (config.DynamicSlots)
            {
                int slotsUsed = players.Connected.Count(player => player.HasPermission(permSlot));
                slotsAvailable = Math.Min(slotCount - slotsUsed, server.MaxPlayers);
            }
            else
            {
                Unsubscribe(nameof(OnServerSave));
                Unsubscribe(nameof(OnUserPermissionRevoked));
                Unsubscribe(nameof(OnUserPermissionRevoked));
            }

            Log(Lang("SlotsNowAvailable", null, slotsAvailable.ToString()));

            if (slotsAvailable >= server.MaxPlayers)
            {
                LogWarning($"Slots available ({slotsAvailable}) is greater than or equal to max players ({server.MaxPlayers})");
            }
        }

        private void OnUserPermissionGranted(string id, string perm)
        {
            if (perm == permSlot && config.DynamicSlots && permission.UserHasPermission(id, permSlot))
            {
                slotsAvailable++;
                Log(Lang("SlotsNowAvailable", null, slotsAvailable.ToString()));
            }
        }

        private void OnUserPermissionRevoked(string id, string perm)
        {
            if (perm == permSlot && config.DynamicSlots && permission.UserHasPermission(id, permSlot))
            {
                slotsAvailable--;
                Log(Lang("SlotsNowAvailable", null, slotsAvailable.ToString()));
            }
        }

        private void OnServerSave() => SaveConfig();

        #endregion Initialization

        #region Reserved Check

        private object CanUserLogin(string name, string id, string ip)
        {
            if (config.AlwaysAllowAdmin)
            {
                IPlayer player = players.FindPlayerById(id);

                if (player != null && player.IsAdmin)
                {
                    Log($"{name} ({id}) is admin, bypassing reserved slot(s)");
                    return null;
                }
            }

            int currentPlayers = players.Connected.Count();
            int maxPlayers = server.MaxPlayers;

            if (slotsAvailable > 0)
            {
                if (maxPlayers - currentPlayers <= slotsAvailable)
                {
                    if (!permission.UserHasPermission(id, permSlot))
                    {
                        return Lang("ReservedSlotsOnly", id);
                    }

                    if (config.KickForReserved && currentPlayers == maxPlayers)
                    {
                        IPlayer[] targets = players.Connected.ToArray();
                        IPlayer target = targets.FirstOrDefault(p => !p.HasPermission(permSlot) && p.Id != id);

                        if (target != null)
                        {
                            target.Kick(Lang("KickedForReserved", target.Id));
                        }
                    }
                }

                if (config.AlwaysUseSlot)
                {
                    slotsAvailable--;
                    Log(Lang("SlotsNowAvailable", null, slotsAvailable.ToString()));
                }
            }

            return null;
        }

        private void OnUserDisconnected(IPlayer player, string reason)
        {
            if (config.AlwaysUseSlot && permission.UserHasPermission(player.Id, permSlot))
            {
                slotsAvailable--;
                Log(Lang("SlotsNowAvailable", null, slotsAvailable.ToString()));
            }
        }

        #endregion Reserved Check

        #region Helper Methods

        private string Lang(string key, string id = null, params object[] args)
        {
            return string.Format(lang.GetMessage(key, this, id), args);
        }

        #endregion Helper Methods
    }
}
