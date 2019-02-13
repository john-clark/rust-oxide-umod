using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("IPBlacklist", "Ankawi", "1.0.1")]
    [Description("Blacklist IP addresses from joining your server")]
    class IPBlacklist : CovalencePlugin
    {
        private const string IPPerm = "ipblacklist.admin";
        private static HashSet<PlayerData> LoadedPlayerData = new HashSet<PlayerData>();

        #region Data
        class PlayerData
        {
            public string PlayerName;
            public string SteamID;
            public string IP;
        }
        private void LoadData() => LoadedPlayerData = Interface.Oxide.DataFileSystem.ReadObject<HashSet<PlayerData>>("IPBlacklist");
        private void SaveData() => Interface.Oxide.DataFileSystem.WriteObject("IPBlacklist", LoadedPlayerData);
        #endregion

        #region Comands

        [Command("banip")]
        private void BanipCommand(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(IPPerm) || !player.IsAdmin)
            {
                player.Reply(GetMsg("NoPermission", player.Id));
                return;
            }
            if (args.Length != 1)
            {
                player.Reply(GetMsg("BanSyntax", player.Id));
                return;
            }
            var target = players.FindPlayer(args[0]);

            if (target == null)
            {
                player.Reply(GetMsg("PlayerNotFound", player.Id, args[0].Sanitize()));
                return;
            }

            LoadedPlayerData.Add(new PlayerData
            {
                PlayerName = target.Name,
                SteamID = target.Id,
                IP = target.Address,
            });
            SaveData();
            target.Kick(GetMsg("IPBlacklisted", target.Id));
            player.Reply("{0} was blacklisted", target.Name);
        }

        [Command("unbanip")]
        private void UnbanipCommand(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(IPPerm) || !player.IsAdmin)
            {
                player.Reply(GetMsg("NoPermission", player.Id));
                return;
            }
            if (args.Length != 1)
            {
                player.Reply(GetMsg("UnbanSyntax", player.Id));
                return;
            }
            var target = players.FindPlayer(args[0]);

            if (target == null)
            {
                player.Reply(GetMsg("PlayerNotFound", player.Id, args[0].Sanitize()));
                return;
            }

            LoadedPlayerData.RemoveWhere(p => p.SteamID == target.Id);
            SaveData();
            player.Reply("{0} was unblacklisted", target.Name);
        }
        #endregion

        #region Functions
        private void Init()
        {
            permission.RegisterPermission(IPPerm, this);
            LoadData();
            LoadDefaultMessages();
        }
        private object CanUserLogin(string name, string id, string ip) => !LoadedPlayerData.Any(p => p.IP == ip || p.SteamID == id);
        //private object CanUserLogin(string name, string id, string ip)
        //{
        //    foreach (var data in LoadedPlayerData)
        //    {
        //        if (data.IP.Contains(ip) || data.SteamID.Contains(id))
        //        {
        //            return GetMsg("IPBlacklisted", id);
        //        }
        //    }
        //    return null;
        //}
        #endregion

        #region Lang
        private void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["IPBlacklisted"] = "You are not allowed to play on this server",
                ["BanSyntax"] = "ipban <target>",
                ["UnbanSyntax"] = "unbanip <target>",
                ["NoPermission"] = "You do not have permission to use this command",
                ["PlayerNotFound"] = "{0} was not found"
            }, this, "en");
        }
        #endregion

        #region Helpers
        private string GetMsg(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);
        #endregion
    }
}