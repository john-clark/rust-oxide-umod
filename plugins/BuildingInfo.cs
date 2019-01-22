using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Oxide.Core;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Building Info", "Iv Misticos", "1.0.4")]
    [Description("Scan buildings and get their owners")]
    class BuildingInfo : RustPlugin
    {
        #region Variables

        private const string PermScan = "buildinginfo.scan";
        private const string PermOwner = "buildinginfo.owner";
        private const string PermAuthed = "buildinginfo.authed";
        private const string PermBypass = "buildinginfo.bypass";
        
        #endregion
        
        #region Configuration
        
        private Configuration _config = new Configuration();

        public class Configuration
        {
            [JsonProperty(PropertyName = "Command Scan")]
            public string CommandScan = "scan";

            [JsonProperty(PropertyName = "Command Scan Owner")]
            public string CommandOwner = "owner";

            [JsonProperty(PropertyName = "Command Scan Authorized Players")]
            public string CommandAuthed = "authed";
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null) throw new Exception();
            }
            catch
            {
                Config.WriteObject(_config, false, $"{Interface.Oxide.ConfigDirectory}/{Name}.jsonError");
                PrintError("The configuration file contains an error and has been replaced with a default config.\n" +
                           "The error configuration file was saved in the .jsonError extension");
                LoadDefaultConfig();
            }

            SaveConfig();
        }

        protected override void LoadDefaultConfig() => _config = new Configuration();

        protected override void SaveConfig() => Config.WriteObject(_config);
        
        #endregion
        
        #region Commands

        private void CommandChatScan(BasePlayer player, string command, string[] args)
        {
            var id = player.UserIDString;
            if (!permission.UserHasPermission(id, PermScan))
            {
                player.ChatMessage(GetMsg("No Permissions", id));
                return;
            }

            var entity = GetBuilding(player);
            if (entity == null)
            {
                player.ChatMessage(GetMsg("Cannot Find", id));
                return;
            }

            var owner = BasePlayer.FindByID(entity.OwnerID);
            if (owner != null && permission.UserHasPermission(owner.UserIDString, PermBypass))
            {
                player.ChatMessage(GetMsg("Scan Unavailable", id));
                return;
            }

            var entities = entity.GetBuildingPrivilege()?.GetBuilding()?.buildingBlocks;
            if (entities == null || entities.Count == 0)
            {
                player.ChatMessage(GetMsg("Cannot Find", id));
                return;
            }

            var dict = new Dictionary<string, ushort>();
            var entitiesCount = entities.Count;
            for (var i = 0; i < entitiesCount; i++)
            {
                var ent = entities[i];
                if (permission.UserHasPermission(ent.OwnerID.ToString(), PermBypass))
                    continue;

                var shortname = ent.ShortPrefabName + $" ({ent.currentGrade.gradeBase.type})";
                if (dict.ContainsKey(shortname))
                    // ReSharper disable once RedundantAssignment
                    dict[shortname]++;
                else
                    dict[shortname] = 1;
            }

            var ex = GetMsg("Scan Info", id);
            var builder = new StringBuilder(GetMsg("Scan Title", id));
            foreach (var el in dict)
            {
                builder.Append(ex);
                builder = builder.Replace("{name}", el.Key).Replace("{amount}", el.Value.ToString());
            }

            player.ChatMessage(builder.ToString());
        }

        private void CommandChatOwner(BasePlayer player, string command, string[] args)
        {
            var id = player.UserIDString;
            if (!permission.UserHasPermission(id, PermScan))
            {
                player.ChatMessage(GetMsg("No Permissions", id));
                return;
            }

            var entity = GetBuilding(player);
            if (entity == null)
            {
                player.ChatMessage(GetMsg("Cannot Find", id));
                return;
            }

            var owner = BasePlayer.FindByID(entity.OwnerID);
            if (owner == null)
            {
                player.ChatMessage(GetMsg("Cannot Find Owner", id));
                return;
            }

            if (permission.UserHasPermission(owner.UserIDString, PermBypass))
            {
                player.ChatMessage(GetMsg("Owner Unavailable", id));
                return;
            }

            player.ChatMessage(GetMsg("Owner Info", id).Replace("{name}", owner.displayName)
                .Replace("{id}", owner.UserIDString));
        }

        private void CommandChatAuthed(BasePlayer player, string command, string[] args)
        {
            var id = player.UserIDString;
            if (!permission.UserHasPermission(id, PermAuthed))
            {
                player.ChatMessage(GetMsg("No Permissions", id));
                return;
            }

            var entity = GetBuilding(player);
            if (entity == null)
            {
                player.ChatMessage(GetMsg("Cannot Find", id));
                return;
            }

            var privilege = entity.GetBuildingPrivilege();
            if (privilege == null)
            {
                player.ChatMessage(GetMsg("Cannot Find Authed", id));
                return;
            }

            if (!privilege.AnyAuthed())
            {
                player.ChatMessage(GetMsg("Authed Zero", id));
                return;
            }

            var ex = GetMsg("Authed Info", id);
            var builder = new StringBuilder(GetMsg("Authed Title", id));
            for (var i = 0; i < privilege.authorizedPlayers.Count; i++)
            {
                var authed = privilege.authorizedPlayers[i];
                builder.Append(ex);
                builder = builder.Replace("{number}", $"{i + 1}").Replace("{name}", authed.username)
                    .Replace("{id}", authed.userid.ToString());
            }
            
            player.ChatMessage(builder.ToString());
        }

        #endregion
        
        #region Hooks

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                { "No Permissions", "You don't have enough permissions." },
                { "Scan Title", "Scan result:" },
                { "Scan Info", "\n{name} x{amount}" },
                { "Scan Unavailable", "Sorry, there was an error. You cannot scan this building." },
                { "Owner Info", "Owner: {name} ({id})" },
                { "Owner Unavailable", "Sorry, there was an error. You cannot get an owner of this building." },
                { "Authed Title", "Authed Players:" },
                { "Authed Info", "\n#{number} - {name} ({id})" },
                { "Authed Unavailable", "" },
                { "Authed Zero", "Nobody is authed here." },
                { "Cannot Find", "Excuse me, where is the building you are looking for?" },
                { "Cannot Find Owner", "Sorry, I don't know who owns this building." },
                { "Cannot Find Authed", "I don't know who is authed there." }
            }, this);
        }

        // ReSharper disable once UnusedMember.Local
        private void Init()
        {
            LoadConfig();

            permission.RegisterPermission(PermScan, this);
            permission.RegisterPermission(PermOwner, this);
            permission.RegisterPermission(PermAuthed, this);
            permission.RegisterPermission(PermBypass, this);

            cmd.AddChatCommand(_config.CommandScan, this, CommandChatScan);
            cmd.AddChatCommand(_config.CommandOwner, this, CommandChatOwner);
            cmd.AddChatCommand(_config.CommandAuthed, this, CommandChatAuthed);
        }

        #endregion
        
        #region Helpers

        private string GetMsg(string key, string userId = null) => lang.GetMessage(key, this, userId);

        private BaseEntity GetBuilding(BasePlayer player)
        {
            RaycastHit info;
            Physics.Raycast(player.eyes.HeadRay(), out info, LayerMask.GetMask("Construction"));
            return info.GetEntity();
        }

        #endregion
    }
}