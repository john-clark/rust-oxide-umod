using System;
using System.Linq;
using System.Collections.Generic;

using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("Sign Ban", "Tori1157", "1.0.1", ResourceId = 2727)]
    [Description("Prevents users from updating signs")]

    class SignBan : CovalencePlugin
    {
        #region Fields

        public const string BanPermission = "signban.banning";

        #endregion Fields

        #region Initializing

        [PluginReference] Plugin SignArtist, ZoneManager;

        private void Init()
        {
            permission.RegisterPermission(BanPermission, this);

            LoadData(ref bannedply);
            SaveData(bannedply);
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Player Try Update"] = "[#red]You are banned from updating signs![/#]",
                ["Player Banning User"] = "You have banned '[#cyan]{0}[/#]' from signs.",
                ["Player Unbanning User"] = "You have unbanned '[#cyan]{0}[/#]' from using signs.",
                ["Target Banned"] = "You have been banned from updating sigs.",
                ["Target Unbanned"] = "You have been unbanned from updating signs.",
                ["Player Already Banned"] = "'[#cyan]{0}[/#]' is already banned.",
                ["Target Not Banned"] = "'[#cyan]{0}[/#]' isn't banned.",
                ["No Permission"] = "You do not have permission to the '[#cyan]{0}[/#]' command!",
                ["Invalid Parameter"] = "'[#cyan]{0}[/#]' is an invalid parameter.",
                ["Invalid Syntax Ban"] = "Invalid Syntax!  |  /sign ban \"user name\"",
                ["Invalid Syntax Unban"] = "Invalid Syntax!  |  /sign unban \"user name\"",
                ["Default Message"] = "This plugin prevents users from updating signs.\n\n[#lightblue]Available Commands:[/#]\n- [#ffa500]/sign[/#] [i](Displays info & help message (This message).)[/i]\n- [#ffa500]/sign ban \"user name\"[/#] [i](Bans user from updating signs.)[/i]\n- [#ffa500]/sign unban \"user name\"[/#] [i](Unbans users from updating signs.)[/i]",
                ["SteamID Not Found"] = "SteamID '[#cyan]{0}[/#]' could not be found.",
                ["Player Not Found"] = "Player '[#cyan]{0}[/#]' could not be found.",
                ["Multiple Players Found"] = "Multiple users found!\n\n{0}",
            }, this);
        }

        #endregion Initializing

        #region Commands

        [Command("sign")]
        private void SignCommand(IPlayer player, string command, string[] args)
        {
            if (!CanBanSign(player) && !player.IsServer)
            {
                SendChatMessage(player, Lang("No Permission", player.Id, command)); 
                return;
            }

            if (args.Length == 0)
            {
                SendChatMessage(player, Lang("Default Message", player.Id));
                return;
            }

            var CommandArg = args[0].ToLower();
            var CommandInfo = $"{command} {args[0]}";
            var CaseArgs = (new List<object>
            {
                "ban", "unban"
            });

            if (!CaseArgs.Contains(CommandArg))
            {
                SendChatMessage(player, Lang("Invalid Parameter", player.Id, CommandInfo));
                return;
            }

            switch (CommandArg)
            {
                #region Ban
                case "ban":

                    if (args.Length == 1)
                    {
                        SendChatMessage(player, Lang("Invalid Syntax Ban", player.Id));
                        return;
                    }

                    IPlayer Btarget;
                    Btarget = GetPlayer(args[1], player);

                    if (Btarget == null) return;

                    if (SignBanInfo.IsSignBanned(Btarget))
                    {
                        SendChatMessage(player, Lang("Player Already Banned", player.Id, Btarget.Name));
                        return;
                    }

                    SendChatMessage(player, Lang("Player Banning User", player.Id, Btarget.Name));
                    SendChatMessage(Btarget, Lang("Target Banned", Btarget.Id));
                    bannedply[Btarget.Id] = new SignBanInfo();
                    SaveData(bannedply);

                return;
                #endregion Ban

                #region Unban
                case "unban":

                    if (args.Length == 1)
                    {
                        SendChatMessage(player, Lang("Invalid Syntax Ban", player.Id));
                        return;
                    }

                    IPlayer Utarget;
                    Utarget = GetPlayer(args[1], player);

                    if (Utarget == null) return;

                    if (!SignBanInfo.IsSignBanned(Utarget))
                    {
                        SendChatMessage(player, Lang("Target Not Banned", player.Id, Utarget.Name));
                        return;
                    }

                    SendChatMessage(player, Lang("Player Unbanning User", player.Id, Utarget.Name));
                    SendChatMessage(Utarget, Lang("Target Unbanned", Utarget.Id));
                    bannedply.Remove(Utarget.Id);
                    SaveData(bannedply);

                return;
                #endregion Unban
            }
        }

        #endregion Commands

        #region Functions

        private bool CanUpdateSign(BasePlayer player, Signage sign)
        {
            if (SignBanInfo.IsSignBanned(player.IPlayer))
            {
                SendChatMessage(player.IPlayer, Lang("Player Try Update", player.UserIDString));
                return false;
            }

            if (ZoneManager != null && ZMNoSignUpdates(player) == true) return false;
            else return true;
        }

        private bool CanBanSign(IPlayer player)
        {
            return (player.HasPermission(BanPermission));
        }

        private object OnPlayerCommand(ConsoleSystem.Arg arg)
        {
            if (arg.Connection == null) return false;
            if (SignArtist == null) return null;

            var command = arg.cmd.FullName;
            var args = arg.GetString(0);
            var player = arg.Connection.player as BasePlayer;

            if (args.StartsWith("/sil") && SignBanInfo.IsSignBanned(player.IPlayer))
            {
                SendChatMessage(player.IPlayer, Lang("Player Try Update", player.UserIDString));
                return false;
            }
            return null;
        }

        #endregion Functions

        #region Helpers

        #region Player Finding
        private IPlayer GetPlayer(string nameOrID, IPlayer player)
        {
            if (nameOrID.IsSteamId() == true)
            {
                IPlayer result = players.All.ToList().Find((p) => p.Id == nameOrID);

                if (result == null)
                    SendChatMessage(player, Lang("SteamID Not Found", player.Id, nameOrID));

                return result;
            }

            List<IPlayer> foundPlayers = new List<IPlayer>();

            foreach (IPlayer current in players.Connected)
            {
                if (current.Name.ToLower() == nameOrID.ToLower())
                    return current;

                if (current.Name.ToLower().Contains(nameOrID.ToLower()))
                    foundPlayers.Add(current);
            }

            switch (foundPlayers.Count)
            {
                case 0:
                    SendChatMessage(player, Lang("Player Not Found", player.Id, nameOrID));
                break;

                case 1:
                    return foundPlayers[0];

                default:
                    string[] names = (from current in foundPlayers select current.Name).ToArray();
                    SendChatMessage(player, Lang("Multiple Players Found", player.Id, string.Join(", ", names)));
                break;
            }
            return null;
        }

        private bool IsParseableTo<T>(object s)
        {
            try
            {
                var parsed = (T)Convert.ChangeType(s, typeof(T));
                return true;
            }
            catch
            {
                return false;
            }
        }
        #endregion

        private static Dictionary<string, SignBanInfo> bannedply = new Dictionary<string, SignBanInfo>();

        public class SignBanInfo
        {
            public static bool IsSignBanned(IPlayer player) => bannedply.ContainsKey(player.Id);

            public SignBanInfo() { }
        }

        private bool ZMNoSignUpdates(BasePlayer player)
        {
            return (bool)ZoneManager.CallHook("EntityHasFlag", player, "nosignupdates");
        }

        private void LoadData<T>(ref T data, string filename = null) => data = Interface.Oxide.DataFileSystem.ReadObject<T>(filename ?? Name);
        private void SaveData<T>(T data, string filename = null) => Interface.Oxide.DataFileSystem.WriteObject(filename ?? Name, data);

        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        #endregion Helpers

        #region Messaging

        private void SendChatMessage(IPlayer player, string message)
        {
            player.Reply(message);
        }

        #endregion Messaging
    }
}