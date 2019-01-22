using System;
using System.Collections.Generic;
using Oxide.Core;
using Newtonsoft.Json;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("MySQL Whitelist", "Sonny-Boi", "1.0.0")]
    [Description("Restricts server access to only those whitelisted in a database")]
    internal class MySQLWhitelist : RustPlugin
    {
        #region Configuration
        private Configuration config;
        class Configuration
        {
            [JsonProperty("host")]
            public string host;
            [JsonProperty("port")]
            public ulong port;
            [JsonProperty("username")]
            public string username;
            [JsonProperty("password")]
            public string password;
            [JsonProperty("database")]
            public string database;

            public static Configuration DefaultConfig()
            {
                return new Configuration()
                {
                    host = "localhost",
                    port = 3306,
                    username = "root",
                    password = "",
                    database = "rust"
                };
            }
        }
        protected override void LoadDefaultConfig()
        {
            PrintWarning("Creating a new configuration file");
            config = Configuration.DefaultConfig();
        }
        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<Configuration>();
        }
        protected override void SaveConfig() => Config.WriteObject(config);
        private void Init()
        {
            LoadConfig();
        }
        #endregion

        private readonly Core.MySql.Libraries.MySql _mySql = Interface.Oxide.GetLibrary<Core.MySql.Libraries.MySql>();
        private Core.Database.Connection _mySqlConnection;
        public List<string> whitelistedPlayers = new List<string>();

        private const string SelectData = "SELECT * FROM whitelist";
        private const string CreateQuery = "CREATE TABLE IF NOT EXISTS `whitelist` ( `id` INT(11) NOT NULL AUTO_INCREMENT , `steamid` VARCHAR(255) NOT NULL, PRIMARY KEY(`id`))";

        private void OnServerInitialized()
        {
            _mySqlConnection = _mySql.OpenDb(Config["host"].ToString(), Convert.ToInt32(Config["port"]), Config["database"].ToString(), Config["username"].ToString(), Config["password"].ToString(), this);
            var sql = Core.Database.Sql.Builder.Append(CreateQuery);
            _mySql.Insert(sql, _mySqlConnection);
            sql = Core.Database.Sql.Builder.Append(SelectData);
            _mySql.Query(sql, _mySqlConnection, list =>
            {
                if (list.Count > 0)
                {
                    foreach (var entry in list)
                    {
                        whitelistedPlayers.Add(entry["steamid"].ToString());
                    }
                }
                PrintWarning("Updated whitelisted users list, checking if players are whitelisted");
            });
        }

        private bool isWhitelisted(string id)
        {
            if (whitelistedPlayers != null && whitelistedPlayers.Contains(id))
                return true;
            return false;
        }

        private void CheckIfWhitelistedOrKick(BasePlayer player)
        {
            if(!isWhitelisted(player.UserIDString) || whitelistedPlayers == null)
            {
                player.Kick(Lang("NotWhitelisted", player.UserIDString));
            }
        }

        private void OnServerSave()
        {
            List<BasePlayer> currentPlayers = BasePlayer.activePlayerList.ToList();
            OnServerInitialized();
            foreach (BasePlayer player in currentPlayers)
                CheckIfWhitelistedOrKick(player);  
        }

        object CanUserLogin(string name, string id)
        {
            if (isWhitelisted(id))
                return true;
            return Lang("NotWhitelisted");
        }

        #region Localization
        private new void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NotWhitelisted"] = "You are not whitelisted!"
            }, this);
        }
        private string Lang(string key, string userId = null, params object[] args) => string.Format(lang.GetMessage(key, this, userId), args);
        #endregion
    }
}
