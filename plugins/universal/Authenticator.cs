using System.Text;
using System.Collections.Generic;
using System.Security.Cryptography;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("Authenticator", "Spicy", "2.0.0")]
    [Description("Provides a simple login system.")]

    class Authenticator : CovalencePlugin
    {
        #region Fields

        private SHA512 sha512;
        private HashSet<string> authenticatedUsers;
        private Data data;
        private int kickTimer;

        #endregion

        #region Methods

        private string Syntax(string syntaxKey) => string.Format(_("InvalidSyntax"), _(syntaxKey));

        private string _(string key) => lang.GetMessage(key, this);

        private byte[] Hash(string password) => sha512.ComputeHash(Encoding.UTF8.GetBytes(password));

        private string ByteArrayToString(byte[] array) => Encoding.UTF8.GetString(array);

        private void Check(IPlayer player)
        {
            if (!authenticatedUsers.Contains(player.Id) && player.IsConnected)
                player.Kick(_("AuthenticationFailure"));
        }

        protected override void LoadDefaultConfig() => Config["KickTimer"] = 60;

        #endregion

        #region Classes

        private class Data
        {
            public Dictionary<string, byte[]> Players;
        }

        #endregion

        #region Hooks

        private void OnServerInitialized()
        {
            sha512 = SHA512.Create();
            authenticatedUsers = new HashSet<string>();
            data = Interface.Oxide.DataFileSystem.ReadObject<Data>(Title);

            if (data.Players == null)
                data.Players = new Dictionary<string, byte[]>();

            kickTimer = Config.Get<int>("KickTimer");

            lang.RegisterMessages(new Dictionary<string, string>()
            {
                ["InvalidSyntax"] = "Invalid syntax. Syntax: {0}.",
                ["SyntaxAuth"] = "/auth [register|unregister|login]",
                ["SyntaxRegister"] = "/auth register [password]",
                ["SyntaxUnregister"] = "/auth unregister [password]",
                ["SyntaxLogin"] = "/auth login [password]",
                ["AlreadyRegistered"] = "You're already registered!",
                ["AlreadyUnregistered"] = "You're already unregistered!",
                ["AlreadyLoggedIn"] = "You're already logged in!",
                ["Registered"] = "You've successfully registered!",
                ["Unregistered"] = "You've successfully unregistered!",
                ["LoggedIn"] = "You've successfully logged in!",
                ["NotRegistered"] = "You're not registered!",
                ["IncorrectPassword"] = "The password you've entered is incorrect.\nPlease contact an administrator if you've forgotten it.",
                ["AuthenticationRequired"] = "Hi, please login with '{0}' or you will be kicked in {1} seconds.",
                ["AuthenticationFailure"] = "You didn't login soon enough."
            }, this);
        }

        private void OnUserConnected(IPlayer player)
        {
            if (data.Players.ContainsKey(player.Id))
            {
                player.Reply(string.Format(_("AuthenticationRequired"), _("SyntaxLogin"), kickTimer));
                timer.Once(kickTimer, () => Check(player));
            }
        }

        private void OnUserDisconnected(IPlayer player, string reason) => authenticatedUsers.Remove(player.Id);

        #endregion

        #region Commands

        [Command("auth")]
        private void Auth(IPlayer player, string command, string[] args)
        {
            if (args.Length == 0 || args == null)
            {
                player.Reply(Syntax("SyntaxAuth"));
                return;
            }

            switch (args[0].ToLower())
            {
                case "register":
                    if (args.Length < 2)
                    {
                        player.Reply(Syntax("SyntaxRegister"));
                        break;
                    }

                    if (data.Players.ContainsKey(player.Id))
                    {
                        player.Reply(_("AlreadyRegistered"));
                        break;
                    }

                    data.Players.Add(player.Id, Hash(args[1]));
                    Interface.Oxide.DataFileSystem.WriteObject(Title, data);
                    player.Reply(_("Registered"));
                    break;

                case "unregister":
                    if (args.Length < 2)
                    {
                        player.Reply(Syntax("SyntaxUnregister"));
                        break;
                    }

                    if (!data.Players.ContainsKey(player.Id))
                    {
                        player.Reply(_("AlreadyUnregistered"));
                        break;
                    }

                    if (ByteArrayToString(Hash(args[1])) != ByteArrayToString(data.Players[player.Id]))
                    {
                        player.Reply(_("IncorrectPassword"));
                        break;
                    }

                    data.Players.Remove(player.Id);
                    Interface.Oxide.DataFileSystem.WriteObject(Title, data);
                    player.Reply(_("Unregistered"));
                    break;

                case "login":
                    if (args.Length < 2)
                    {
                        player.Reply(Syntax("SyntaxLogin"));
                        break;
                    }

                    if (authenticatedUsers.Contains(player.Id))
                    {
                        player.Reply(_("AlreadyLoggedIn"));
                        break;
                    }

                    if (!data.Players.ContainsKey(player.Id))
                    {
                        player.Reply(_("NotRegistered"));
                        break;
                    }

                    if (ByteArrayToString(Hash(args[1])) != ByteArrayToString(data.Players[player.Id]))
                    {
                        player.Reply(_("IncorrectPassword"));
                        break;
                    }

                    authenticatedUsers.Add(player.Id);
                    player.Reply(_("LoggedIn"));
                    break;

                default:
                    player.Reply(Syntax("SyntaxAuth"));
                    break;
            }
        }

        #endregion
    }
}