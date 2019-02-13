using System.Collections.Generic;   //dic
using Oxide.Core.Configuration;
using Oxide.Core;
using Convert = System.Convert;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("Playtime Supply Signal", "BuzZ[PHOQUE]", "0.0.4")]
    [Description("Give player a Supply Signal, based on time played on server and configurable rate.")]

/*======================================================================================================================= 
*
*   CHAT COMMAND on this version : none.
*   11th september 2018
*
*   THANKS TO THE OXIDE/UMOD TEAM for coding quality, ideas, and time spent for the community
*
*   
*=======================================================================================================================*/

    public class PlaytimeSupplySignal : RustPlugin
    {
		private Timer clock;
        bool debug = false;
        bool ConfigChanged;

        float clockrate = 120;      // 120 seconds by default -> 2min.
        float bonusratemin = 60;     // 3600 -> for each hour played by default
        bool spawnbonus = true;
        string Prefix = "[PSS] ";                       // CHAT PLUGIN PREFIX
        string PrefixColor = "#555555";                 // CHAT PLUGIN PREFIX COLOR
        string ChatColor = "#999999";                   // CHAT MESSAGE COLOR
        ulong SteamIDIcon = 76561198859649789;          // STEAMID CREATED FOR THIS PLUGIN 76561198859649789

    class StoredData
    {
        public Dictionary<ulong, bool> playerIDhadfirst = new Dictionary<ulong, bool>();
        public Dictionary<ulong, float> playerIDbonusclock = new Dictionary<ulong, float>();
        public List<ulong> playerIDpending = new List<ulong>();

        public StoredData()
        {
        }
    }

    private StoredData storedData;

#region CONFIG

    protected override void LoadDefaultConfig()
        {
            LoadVariables();
        }

        private void LoadVariables()
        {
            
            Prefix = Convert.ToString(GetConfig("Chat Settings", "Prefix", "[PSS] :"));                       // CHAT PLUGIN PREFIX
            PrefixColor = Convert.ToString(GetConfig("Chat Settings", "PrefixColor", "#42d7f4"));                // CHAT PLUGIN PREFIX COLOR
            ChatColor = Convert.ToString(GetConfig("Chat Settings", "ChatColor", "#b7f5ff"));                    // CHAT  COLOR
            SteamIDIcon = Convert.ToUInt64(GetConfig("Chat Settings", "SteamIDIcon", 76561198859649789));   
            bonusratemin = Convert.ToSingle(GetConfig("Bonus Playtime", "Value in minutes", "60"));      
            spawnbonus = Convert.ToBoolean(GetConfig("Bonus on Spawn", "Give a SupplySignal on first arrival", "true"));      

            if (!ConfigChanged) return;
            SaveConfig();
            ConfigChanged = false;
        }

        private object GetConfig(string menu, string datavalue, object defaultValue)
        {
            var data = Config[menu] as Dictionary<string, object>;
            if (data == null)
            {
                data = new Dictionary<string, object>();
                Config[menu] = data;
                ConfigChanged = true;
            }
            object value;
            if (!data.TryGetValue(datavalue, out value))
            {
                value = defaultValue;
                data[datavalue] = value;
                ConfigChanged = true;
            }
            return value;
        }

#endregion

#region INIT

    void Init()
    {
        storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Name);
        LoadVariables();
    }

    void OnServerInitialized()
    {
            float bonusrate = (bonusratemin * 60);
        if (bonusrate <= (clockrate+1))
        {
            PrintError("Please set a longer rate for the bonus. Minimum rate is 3 min.");
            return;
        }
		clock = timer.Repeat(clockrate, 0, () =>
		{
            LetsClockOnActivePlaytime();
            //LetsClockOnSleeperPlaytime(); // for test
		});
    }

#endregion

#region UNLOAD

		void Unload()
		{
            Interface.Oxide.DataFileSystem.WriteObject(Name, storedData);
		}

#endregion

#region MESSAGES

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"FreeSlotMsg", "Please free one inventory slot to receive a bonus !"},
                {"RxMsg", "You received a SupplySignal bonus for your playtime !"},
                {"SpawnBonusMsg", "You received a SupplySignal bonus for joining this server !"},
                {"ItemMsg", "BONUS Playtime Supply Signal"},
                
            }, this, "en");

            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"FreeSlotMsg", "Veuillez libérer une case d'inventaire pour recevoir un bonus !"},
                {"RxMsg", "Vous venez de recevoir un signal fumigène comme récompense de temps joué !"},
                {"SpawnBonusMsg", "Vous venez de recevoir un signal fumigène pour avoir choisi ce serveur !"},
                {"ItemMsg", "signal BONUS pour temps de jeu"},

            }, this, "fr");
        }

#endregion

        void LetsClockOnActivePlaytime()
        {
            List<BasePlayer> playerlist = BasePlayer.activePlayerList.ToList();
            CountDaTime(playerlist);
        }

        void LetsClockOnSleeperPlaytime()
        {
            List<BasePlayer> playerlist = BasePlayer.sleepingPlayerList.ToList();
            CountDaTime(playerlist);
        }

        void CountDaTime(List<BasePlayer> playerlist)
        {
            foreach (BasePlayer player in playerlist)
            {
                float bonusrate = (bonusratemin * 60);
                if (debug){Puts($"rate in seconds, calculated from config : {bonusrate}");}
                if (storedData.playerIDhadfirst.ContainsKey(player.userID) == false)
                {
                    if (spawnbonus == true)
                    {
                        Player.Message(player, $"<color={ChatColor}> {lang.GetMessage("SpawnBonusMsg", this, player.UserIDString)}</color>",$"<color={PrefixColor}> {Prefix} </color>", SteamIDIcon);
                        GiveSupplyDropToPlayer(player);
                        if (debug){Puts($"player {player.userID} did -NOT- received bonus yet -> GIVE a supplydrop");}
                    }
                    storedData.playerIDbonusclock.Add(player.userID, 0);
                    storedData.playerIDhadfirst.Add(player.userID, true);                        
                }
                else
                {
                    if (debug){Puts($"player -DID- receive bonus");}
                    float oldtime;
                    storedData.playerIDbonusclock.TryGetValue(player.userID, out oldtime);
                    storedData.playerIDbonusclock.Remove(player.userID);
                    float newtime = oldtime + clockrate;
                    storedData.playerIDbonusclock.Add(player.userID, newtime);
                    if (debug){Puts($"player {player.userID} newtime {newtime}");}
                    if (bonusrate <= 0)
                    {
                        if (debug){Puts($"error : bonusrate <= 0. has been set to 1 hour");}
                        bonusrate = 3600;
                    }
                    if (newtime >= bonusrate)
                    {
                        if (debug){Puts($"player {player.userID} reached bonusrate");}
                        storedData.playerIDbonusclock.Remove(player.userID);
                        storedData.playerIDbonusclock.Add(player.userID, 0);
                        GiveSupplyDropToPlayer(player);
                    }
                }
            }
        }

#region GIVE

        void GiveSupplyDropToPlayer(BasePlayer player)
        {
            foreach (var playerzactiv in BasePlayer.activePlayerList.ToList())
            {
                if (playerzactiv == player)
                {
                    Item itemtogive = ItemManager.CreateByItemID(ItemManager.FindItemDefinition(1397052267).itemid, 1);
                    if (itemtogive == null)
                    {
                        if (debug){Puts($"Error on item creation at #GIVE");}
                        return;
                    }
                    itemtogive.name = $"<color=purple>{lang.GetMessage("ItemMsg", this, player.UserIDString)}</color>";
                    int invcount = player.inventory.containerMain.itemList.Count;
                    int freeslot = 24 - invcount;
                    if (debug){Puts($"freeslot {freeslot}");}
                    if (freeslot < 1)
                    {
                        storedData.playerIDpending.Add(player.userID);
                        if (debug){Puts($"inventory full");}
                        Player.Message(player, $"<color={ChatColor}> {lang.GetMessage("FreeSlotMsg", this, player.UserIDString)}</color>",$"<color={PrefixColor}> {Prefix} </color>", SteamIDIcon);                
                        return;
                    }
                    if (freeslot >= 1)
                    {
                        Player.Message(player, $"<color={ChatColor}> {lang.GetMessage("RxMsg", this, player.UserIDString)}</color>",$"<color={PrefixColor}> {Prefix} </color>", SteamIDIcon);                
                        if (debug){Puts($"GIVING ITEM");}
                        player.GiveItem(itemtogive);
                        if (storedData.playerIDpending.Contains(player.userID) == true)
                        {
                            storedData.playerIDpending.Remove(player.userID);
                        }
                        return;
                    }
                }
            }
        }
#endregion 
    }
}