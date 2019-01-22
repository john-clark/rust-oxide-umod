using System.Collections.Generic;
using System;
using System.Text;
using Oxide.Core;
using Oxide.Core.MySql;
using Oxide.Core.Plugins;
using Oxide.Core.Database;
using MySql.Data.MySqlClient;

namespace Oxide.Plugins
{
    [Info("EMSQL", "Steenamaroo", "0.0.5", ResourceId = 2442)] 
    class EMSQL : RustPlugin
    {                                                              
        class DataStorage
        {
            public Dictionary<ulong, EMDATA> stats = new Dictionary<ulong, EMDATA>();
            public DataStorage() { }
        }
            
        class EMDATA
        {
            public string Name;
            public int Kills;
            public int Deaths;
            public int GamesPlayed;
            public int GamesWon;
            public int GamesLost;
            public double Score;
            public int Rank;
            public int FlagsCaptured;
            public int ShotsFired;
            public int ChoppersKilled;
        }
        
        DataStorage data;

        void OnServerInitialized()
        {
            LoadData();
            LoadConfigVariables();
            SaveConfig();
            LoadMySQL();
            timer.Once(savetimer * 60, () => OnServerInitialized());
        }

    public static string RemoveSurrogatePairs(string str, string replacementCharacter = "?")
    {
        if (str == null)
        {
            return null;
        }
        StringBuilder sb = null;
        for (int i = 0; i < str.Length; i++)
        {
            char ch = str[i];
    
            if (char.IsSurrogate(ch))
            {
                if (sb == null)
                {
                    sb = new StringBuilder(str, 0, i, str.Length);
                }
                sb.Append(replacementCharacter);
    
                if (i + 1 < str.Length && char.IsHighSurrogate(ch) && char.IsLowSurrogate(str[i + 1]))
                {
                    i++;
                }
            }
            else if (sb != null) 
            {
                sb.Append(ch);
            }
        }
        
        return sb == null ? str : sb.ToString();
    }

        protected override void LoadDefaultConfig() 
        {
            Puts("Creating a new config file");
            Config.Clear();
            LoadConfigVariables();
            SaveConfig();
        }

        Core.MySql.Libraries.MySql Sql = Interface.GetMod().GetLibrary<Core.MySql.Libraries.MySql>(); 
        Connection Sql_conn;

        void LoadMySQL()
        {
            try
            {
                Sql_conn = Sql.OpenDb(sql_host, sql_port, sql_db, sql_user, sql_pass, this);
                if (Sql_conn == null || Sql_conn.Con == null)
                {
                    Puts("SQL connection is not open.");     
                    return; 
                }
                Sql.Insert(Core.Database.Sql.Builder.Append($"CREATE TABLE IF NOT EXISTS {tablename} ( `UserID` VARCHAR(40) NOT NULL, `Name` VARCHAR(40) NOT NULL, `Kills` INT(11) NOT NULL, `Deaths` INT(11) NOT NULL, `GamesPlayed` INT(11) NOT NULL, `GamesWon` INT(11) NOT NULL, `GamesLost` INT(11) NOT NULL, `Score` INT(11) NOT NULL, `Rank` INT(11) NOT NULL, `FlagsCaptured` INT(11) NOT NULL, `ShotsFired` INT(11) NOT NULL, `ChoppersKilled` INT(11) NOT NULL, PRIMARY KEY (`UserID`) );"), Sql_conn);
                    Puts("Creating or Updating EM Stats Table");  
            } 
            catch (Exception e)
            { 
                Puts("SQL Save failed.");
            }  
            foreach(var c in data.stats)  
            {
                Sql.Insert(Core.Database.Sql.Builder.Append($"INSERT INTO {tablename} ( `UserID`, `Name`, `Kills`, `Deaths`, `GamesPlayed`, `GamesWon`, `GamesLost`, `Score`, `Rank`, `FlagsCaptured`, `ShotsFired`, `ChoppersKilled`) VALUES ( @0, @1, @2, @3, @4, @5, @6, @7, @8, @9, @10, @11) ON DUPLICATE KEY UPDATE UserID = @0, Name = @1, Kills = @2, Deaths = @3, GamesPlayed = @4, GamesWon = @5, GamesLost = @6, Score = @7, Rank = @8, FlagsCaptured = @9, ShotsFired = @10, ChoppersKilled = @11", c.Key, RemoveSurrogatePairs(c.Value.Name, ""), c.Value.Kills, c.Value.Deaths, c.Value.GamesPlayed, c.Value.GamesWon, c.Value.GamesLost, c.Value.Score, c.Value.Rank, c.Value.FlagsCaptured, c.Value.ShotsFired, c.Value.ChoppersKilled), Sql_conn); 
            } 
        }   

        #region config
        string sql_host = "";
        int sql_port = 3306;
        string sql_db = "";
        string sql_user = "";
        string sql_pass = "";
        int savetimer = 15;
        string tablename = "emstatsdb";

        private void LoadConfigVariables()
        {
            CheckCfg("MySQL - Host", ref sql_host);
            CheckCfg("MySQL - Port", ref sql_port);
            CheckCfg("MySQL - Database Name", ref sql_db);
            CheckCfg("MySQL - Table Name", ref tablename);
            CheckCfg("MySQL - Username", ref sql_user);
            CheckCfg("MySQL - Password", ref sql_pass);
            CheckCfg("MySQL - Save Timer", ref savetimer);            
        }
          
        private void CheckCfg<T>(string Key, ref T var)
        {
            if (Config[Key] is T)
                var = (T)Config[Key];
            else
                Config[Key] = var;
        }
        #endregion

        void LoadData()
        {
            data = Interface.GetMod().DataFileSystem.ReadObject<DataStorage>("EventManager/Statistics");
        }
    }
}