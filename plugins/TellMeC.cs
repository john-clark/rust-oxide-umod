using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Rust;
using Oxide.Core.Plugins;
using System.Globalization;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("Tell Me C", "BuzZ[PHOQUE]", "0.0.3")]
    [Description("Tell THE good color pair you see, and get Ammo and RP Points")]
    public class TellMeC: RustPlugin

/*==============================================================================================================
*    REWARD IS SET ON AMMO - ALL GAME TYPES RANDOMISED EXCEPT ROCKETS - IN 6 RANDOMIZED QUANTITY FROM 5 TO 30
*    REWARD IS ALSO RP POINTS - (5 FOR WIN - 1 FOR LOSE) BY DEFAULT. SEE CONFIG FILE.
*    
*    THANKS A LOT TO THE OXIDE TEAM for coding quality and time spent for community.
*    
 ==============================================================================================================*/

    {
        [PluginReference]     
        Plugin ServerRewards;                   // http://oxidemod.org/plugins/serverrewards.1751/

#region DECLARATION & DEBUG

        string ColorToFind;
        string WinWord;
        List<ulong> TellMeCPlayerIDs; 
        private bool ConfigChanged;
        private string ItemWon = "";
        private string ItemToWin = "";
        int QuantityToWin;                              // RANDOMISED QUANTITY TO APPLY TO RANDOMISED ITEM TO WIN
        bool TellMeCIsOn;                               // TRUE WHEN GAME IS ON
        string MixC;
        string finalsentence;
        float TellMeCRate = 600;
        float TellMeCLength = 25;
        //float TellMeCEndTime;                           // FOR WHEN IT ENDS. VALUE GIVEN AT START VOID                                                         
        //float NextTellMeCTime;                          // FOR TIME UNTIL NEXT GAME                                   
        private string ToWait;

        bool debug = false;

#endregion

#region CUSTOM

// POUR LA CUSTOMISATION DU PLUGIN
        string Prefix = "[TellMeC] ";                   // CHAT PLUGIN PREFIX
        string PrefixColor = "#71c2fc";                 // CHAT PLUGIN PREFIX COLOR
        ulong SteamIDIcon = 76561198842641699;          // SteamID FOR PLUGIN ICON
        string WinnerColor = "#4fffc7";                 // WINNER NAME COLOR
        private bool UseServerRewards = false;
        int RPOnWin = 5;                                // RP POINTS ADDED ON WIN
        int RPOnLose = 1;                               // RP ADDED ON LOSE   

// STEAM PROFILE CREATED FOR THIS PLUGIN : ID = 76561198842641699

#endregion

#region DICTIONNAIRES

// DICTIONNAIRE DES ITEMS A GAGNER
        Dictionary<int, string> Item = new Dictionary<int, string>()        // ITEMS TO RANDOMIZE
        {
            [0] = "ammo.pistol",
            [1] = "ammo.pistol.fire",
            [2] = "ammo.pistol.hv",
            [3] = "ammo.rifle",
            [4] = "ammo.rifle.explosive",
            [5] = "ammo.rifle.hv",
            [6] = "ammo.rifle.incendiary",
            [7] = "ammo.shotgun",
            [8] = "ammo.shotgun.slug",
            [9] = "ammo.handmade.shell",            
        };

// dictionnaire des quantites a tirer au hasard
        Dictionary<int, int> Quantity = new Dictionary<int, int>()
        {
            [0] = 5,
            [1] = 10,
            [2] = 15,
            [3] = 20,           
            [4] = 25,           
            [5] = 30,           
        };

        Dictionary<string, string> colorRGB = new Dictionary<string, string>()
        {
               { "#2d2d2d" , "black" },
               { "#00c5e8" , "blue" },
               { "#54ff68" , "green" },
               { "#a3a3a3" , "grey" },
               { "#ffb163" , "orange" },
               { "#bf64fc", "purple" },
               { "#ff726b" ,"red" },
               { "white" , "white" },
               { "#fffc75" ,"yellow" },   
        };
        
        Dictionary<string, string> randomed = new Dictionary<string, string>();    
        List<String> mixRGB = new List<string>();
        List<String> mixTOSEE = new List<string>();

#endregion

#region MESSAGES

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"StartTellMeCMsg", "Tell to me THE good pair you see ! (ex: <color=yellow>/color white</color>)"},
                {"NextTellMeCMsg", "The game restarts every "},
                {"AlreadyTellMeCMsg", "You have already played on this round !\n"},
                {"InvalidTellMeCMsg", "Invalid entry.\nTry something like /c white"},
                //{"NotAlphaTellMeCMsg", "Invalid guess.\nTry something like <color=yellow>/x 1234</color>"},
                {"WonTellMeCMsg", "did find the color to see !\nand has won :"},
                {"EndTellMeCMsg", "The good color was "},
                {"ExpiredTellMeCMsg", "was not found in time !"},
                {"LoseTellMeCMsg", "This is NOT THE RIGHT COLOR... for trying you won "},
                {"SorryErrorMsg", "Sorry an error has occured ! Please Tell BuzZ[PHOQUE] about this Thank you !. Item to give was null. gift was : "},

            }, this, "en");

            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"StartTellMeCMsg", "Un mot a sa bonne couleur, lequel ?! (exemple: <color=yellow>/color white</color>)"},
                {"NextTellMeCMsg", "Un tirage de mots colorés a lieu toutes les "},
                {"AlreadyTellMeCMsg", "Vous avez déjà essayé sur ce tour !\n"},
                {"InvalidTellMeCMsg", "Saisie invalide.\nEssayez quelque chose comme /c white"},
                //{"NotAlphaTellMeCMsg", "Valeur invalide.\nEssayez dans ce format<color=yellow>/c white</color>"},
                {"WonTellMeCMsg", "a trouvé la bonne couleur !\net a gagné :"},
                {"EndTellMeCMsg", "La couleur était : "},
                {"ExpiredTellMeCMsg", "n'a pas été trouvée à temps !"},
                {"LoseTellMeCMsg", "Ce n'est pas la bonne couleur... pour cet essai, vous gagnez "},
                {"SorryErrorMsg", "Désolé il y a eu une erreur ! Touchez en un mot à BuzZ[PHOQUE] Merci !. L'obet à donner est null. 'gift' est : "},

            }, this, "fr");
        }

#endregion

#region CONFIG

    protected override void LoadDefaultConfig()
        {
            LoadVariables();
        }

        private void LoadVariables()
        {
            Prefix = Convert.ToString(GetConfig("Chat Settings", "Prefix", "[TellMeC] "));                       // CHAT PLUGIN PREFIX
            SteamIDIcon = Convert.ToUInt64(GetConfig("Settings", "SteamIDIcon", "76561198842641699"));        // SteamID FOR PLUGIN ICON - STEAM PROFILE CREATED FOR THIS PLUGIN / 76561198842176097 /
            WinnerColor = Convert.ToString(GetConfig("Chat Settings", "Color For Winner Name", "#4fffc7"));                // WINNER NAME COLOR
            UseServerRewards = Convert.ToBoolean(GetConfig("Rewards Settings", "Use Server Rewards", "false"));
            RPOnWin = Convert.ToInt32(GetConfig("Rewards Settings", "RP Points on Win", "5"));
            RPOnLose = Convert.ToInt32(GetConfig("Rewards Settings", "RP Points on Lose", "1"));
            TellMeCRate = Convert.ToSingle(GetConfig("Game repeater", "Rate in seconds", "600"));
            TellMeCLength = Convert.ToSingle(GetConfig("Game length", "in seconds", "25"));

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

#region SERVER REWARDS PLUGIN VERIFICATION DU .CS ET ERROR
        void Loaded()
        {
            if (UseServerRewards == true)
                {
                    if (ServerRewards == false) PrintError("ServerRewards.cs is not present. Change your config option to disable RP rewards and reload TellMeC. Thank you.");
                }

            if (TellMeCLength >= TellMeCRate) PrintError("Game length is bigger than game rate. Change your config options in seconds and reload TellMeC. Thank you.");
        }

#endregion

#region EXPIRATION

        void TellMeCExpired()
        {
            Server.Broadcast($"Color {MixC} {lang.GetMessage("ExpiredTellMeCMsg", this)}",Prefix, SteamIDIcon); 
            TellMeCIsOn = false;    
            //NextTellMeCTime = Time.time + TellMeCRate;
            TellMeCPlayerIDs.Clear();
        }
#endregion

#region ON SERVER INIT

		private void Init()
        {
            randomed.Clear();
            mixRGB.Clear();
            mixTOSEE.Clear();
            LoadVariables();
            //NextTellMeCTime = Time.time + TellMeCRate;


        }

        private void OnServerInitialized()
        {
            timer.Every(TellMeCRate, () =>
            {
                    StartTellMeC(); 
            });
        }
#endregion

#region TELLMEC

        private void StartTellMeC()
        {
            TellMeCPlayerIDs = new List<ulong>();
// COLORTOFIND RANDOM ON COLOR RGB DICO transformed to ListArray By Sir Randomizer
            string ColorToFound = Randomizer();
            ColorToFind = ColorToFound;
// TRANSFORM THIS COLORTOFIND STRING IN A COLOR TO SEE            
            colorRGB.TryGetValue(ColorToFind, out WinWord);               
            if (debug) Puts($" Win value in RGB : {ColorToFind}; in clear to see {WinWord}");
// ecrire dans le dico randomed  = ColorToFind, WinWord; et string MixC qui sera todisplay 
            MixC = $"<color={ColorToFind}>{WinWord}</color>";
            BuildMix();
            string[] mixRGBstring = mixRGB.ToArray();
            string[] mixTOSEEstring = mixTOSEE.ToArray();
            List<string> mixTOSEEconvert = new List<string>();
// -> transform en readable
            int MixRound = 1;
            int MixDisplayed = 15;
            int MixToRandom = 15;
            List<string> mixtodisplay = new List<string>();
            mixtodisplay.Add(MixC);
            for (MixRound = 1; MixRound <= MixDisplayed; MixRound++)
            {
                int round = MixRound -1;
                string tosee;
                string WordZ = mixTOSEEstring[round];
                colorRGB.TryGetValue(WordZ, out tosee);
                mixTOSEEconvert.Add(tosee);
            }
            string[] mixTOSEEclear = mixTOSEEconvert.ToArray();
            MixRound=1;
            for (MixRound = 1; MixRound <= MixDisplayed; MixRound++)
            {
                int round = MixRound -1;
                string MixYold;
                string ColorY = mixRGBstring[round];
                string WordY = mixTOSEEclear[round];
                string MixY = $"<color={ColorY}> {WordY}</color>";
                mixtodisplay.Add(MixY);
                if (debug) Puts($"STEP {MixRound} MIX DONE : {MixY}");
            }
// MIX C IS ALREADY SET TO COLOR TO FIND
            if (debug) Puts($"MIX C FOR THE WINNER TO SEE {MixC}");
            List<string> sentence = new List<string>();
            MixRound=1;
            int lines = mixtodisplay.Count;
            if (debug) Puts($"nombre a display {lines.ToString()}");
            for (MixRound = 1; MixRound <= lines; MixRound++)
            {   
                MixRound=MixRound -1;
                int RandomLine = Core.Random.Range(0, lines);
                string mixed = mixtodisplay[RandomLine];
                if (debug)
                {
                    Puts($"lignes restantes {lines.ToString()}");
                    Puts($"string mixed {mixed}");
                }
                lines = mixtodisplay.Count - 1;
                sentence.Add(mixed);
                mixtodisplay.RemoveAt(RandomLine);
            }
            string[] sentenceTOSEE = sentence.ToArray();
            finalsentence =$"{sentenceTOSEE[0]}, {sentenceTOSEE[1]}, {sentenceTOSEE[2]}, {sentenceTOSEE[3]}, {sentenceTOSEE[4]}, {sentenceTOSEE[5]}, {sentenceTOSEE[6]}, {sentenceTOSEE[7]}\n{sentenceTOSEE[8]}, {sentenceTOSEE[9]}, {sentenceTOSEE[10]}, {sentenceTOSEE[11]}, {sentenceTOSEE[12]}, {sentenceTOSEE[13]}, {sentenceTOSEE[14]}, {sentenceTOSEE[15]}";
            if (debug) Puts($"{finalsentence}");
            BroadcastSentence(true);
            Puts($"TellMeC has started. The color to see is : {WinWord}");
            TellMeCIsOn = true;
            timer.Once(TellMeCLength, () =>
            {
                if (TellMeCIsOn) TellMeCExpired();
            });
            int RandomQuantity = Core.Random.Range(0,6);                       // correpsond au nombre de choix de calcul
            int RandomItem = Core.Random.Range(0,10);
            ItemToWin = Item[RandomItem];                                       // appliquer le choix suivant le precedent nombre
            QuantityToWin = Quantity[RandomQuantity];
        }

#endregion

#region BUILD THE MIX

        public void BuildMix()
        {
            int MixRound = 1;
            int WordsDisplayed = 14;
            for (MixRound = 1; MixRound <= WordsDisplayed; MixRound++)
            {
                string ColorX = Randomizer();
                string WordX = Randomizer();
                string IsAlready;
                if (ColorX == WordX)
                {
                    MixRound = MixRound -1;
                    WordsDisplayed = WordsDisplayed +1;
                    if (debug) Puts($"STEP {MixRound} , EQUALITY.");
                    continue;
                }
                mixRGB.Add(ColorX);
                mixTOSEE.Add(WordX);
                if (debug) Puts($"STEP {MixRound} , PAIR {ColorX} - {WordX} ADDED TO DICO.");
            }
        }

        string Randomizer()
        {
            int RandomRGB;
            RandomRGB = Core.Random.Range(0, 8);                          // NOMBRE CHOIX ITEM
            List<String> RGBKeys = colorRGB.Keys.ToList();
            string[] RGBstring = RGBKeys.ToArray();
            string ColorToFinder = RGBstring[RandomRGB];
            return(ColorToFinder);
        }

#endregion

#region BROADCAST

// BROADCAST

        void BroadcastSentence(bool start)
        {
            if (start) Server.Broadcast($"{lang.GetMessage("StartTellMeCMsg", this)}\n{finalsentence}",Prefix, SteamIDIcon);
            else Server.Broadcast($"{lang.GetMessage("EndTellMeCMsg", this)} {MixC}",Prefix, SteamIDIcon);
        }        

#endregion

#region CHAT COMMAND /color

// PLAYER CHAT COMMAND
        [ChatCommand("color")]                                                              // SUR COMMANDE CHAT
        void TellMeCCommand(BasePlayer player, string command, string[] args)
        {
            if (!TellMeCIsOn)                                                           // si le jeu n est pas en cours, on donne le next time - message chat player
            {
                //float ToNext = Time.time - NextTellMeCTime;                             // CALCUL INTERVALLE EN SECONDES
                //ToWait = ToNext.ToString("######;######");                              // ARRONDI ET SUPPRESSION DU NEGATIF
                Player.Message(player, $"{lang.GetMessage("NextTellMeCMsg", this, player.UserIDString)} {TellMeCRate} seconds",Prefix, SteamIDIcon);
                return;
            }

            if(TellMeCPlayerIDs.Contains(player.userID))                                // si le ID du joueur a deja ete enregistré, on dis d'attendre, et on donne le next time
            {
                //float ToNext = TellMeCRate - (Time.time - NextTellMeCTime) + TellMeCRate;
                //ToWait = ToNext.ToString("######");
                Player.Message(player, $"{lang.GetMessage("AlreadyTellMeCMsg", this, player.UserIDString)}",Prefix, SteamIDIcon);
                return;
            }

            if(args.Length != 1)                                                        // si les arguments sont vides        
            {
                Player.Message(player, $"{lang.GetMessage("InvalidTellMeCMsg", this, player.UserIDString)}",Prefix, SteamIDIcon);                
                return;
            }
            string answer = args[0];
            if (answer.ToLower().Contains(WinWord))                                                 // WINNER SI X EST DANS LES ARGS DE LA COMMANDE PLAYER
            {
                TellMeCIsOn = false;                                                    // GAME IS !ON
                //NextTellMeCTime = Time.time + TellMeCRate;                              // NEXT TIME = TIMER + RATE
                TellMeCPlayerIDs.Clear();                                               // VIDE LA LISTE DES JOUEURS
                GivePlayerGift(player, ItemToWin);                                      // EXECUTE GIVEPLAYER AVEC player ET ItemToWin
                if (UseServerRewards == true)
                {
                    ServerRewards?.Call("AddPoints", player.userID, (int)RPOnWin);          // HOOK VERS PLUGIN ServerRewards POUR ADD RPWin
                    Server.Broadcast($"<color={WinnerColor}>{player.displayName}</color> {lang.GetMessage("WonTellMeCMsg", this, player.UserIDString)} [{ItemWon}] + [{RPOnWin}.RP]",Prefix, SteamIDIcon); 
                }
                else
                {    
                    Server.Broadcast($"<color={WinnerColor}>{player.displayName}</color> {lang.GetMessage("WonTellMeCMsg", this, player.UserIDString)} [{ItemWon}]",Prefix, SteamIDIcon); 
                }
                BroadcastSentence(false);                                                   // PASSE LE VOID BROADCAST A FALSE
            }
            else                                                                        // WE COULD SAY LOSER
            {        
                if (UseServerRewards)
                {                
                    ServerRewards?.Call("AddPoints", player.userID, (int)RPOnLose);         // HOOK VERS PLUGIN ServerRewards POUR ADD RPLose
                    Player.Message(player, $"{lang.GetMessage("LoseTellMeCMsg", this, player.UserIDString)} [{RPOnLose}.RP]",Prefix, SteamIDIcon);               
                }
                TellMeCPlayerIDs.Add(player.userID);                                    // ADD PLAYER TO THOSE WHO TRIED TO FIND X
            }
        }

#endregion

#region GIVE TO PLAYER
        void GivePlayerGift(BasePlayer player, string gift)
        {
            Item item = ItemManager.CreateByItemID(ItemManager.FindItemDefinition(gift).itemid,QuantityToWin);
            if (item == null)
            {
                Player.Message(player, $"{lang.GetMessage("SorryErrorMsg", this)} {ItemToWin}",Prefix, SteamIDIcon);               
                return;
            }
            player.GiveItem(item);
            ItemWon = $"{QuantityToWin} x {gift}";
        }
#endregion
    }
}





