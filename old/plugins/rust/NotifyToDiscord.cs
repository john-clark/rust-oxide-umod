using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using Rust;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEngine;
using Oxide.Core.Libraries.Covalence;
using Newtonsoft.Json;
using System.Net;

namespace Oxide.Plugins
{
    [Info("Notify To Discord", "BuzZ[PHOQUE]", "0.0.10")]
    [Description("Choose from a large range of server/player notifications to message to a Discord Channel")]

/*======================================================================================================================= 
*
*   
*   THANKS TO THE OXIDE/UMOD TEAM for coding quality, ideas, and time spent for the community
*
*   v0.0.8 option depend DiscordMessages    added in config webhook true/false and URL
*
*   THERE IS A COUNTER OVERFLOW QUEUE to avoid discord cache drowned (each message will be at least 1 second separeted).
*   And it cancel the notification if it's already in recent queue.
*   Adds a TimeStamp on messages
*   
*
*=======================================================================================================================*/
    public class NotifyToDiscord : RustPlugin
    {
        [PluginReference]     
        Plugin BetterChat,HooksExtended,DangerousTreasures,HumanNPC,DiscordMessages; 

        bool debug = false;

        string Prefix = "[NTD] :";                       // CHAT PLUGIN PREFIX
        string PrefixColor = "#42d7f4";                 // CHAT PLUGIN PREFIX COLOR
        string ChatColor = "#b7f5ff";                   // CHAT MESSAGE COLOR
        ulong SteamIDIcon = 76561198044414155;          // SteamID FOR PLUGIN ICON
        bool countonline = true;
        string WebHookURL = string.Empty;
        bool WebHook = false;

        public List<string> queue = new List<string>();
        private static string BaseURLTemplate = "https://discordapp.com/api/channels/{{ChannelID}}/messages";
        bool discording;

#region HOOKS BOOL

        bool addvendingoffer, airdrop, apchunt, onattack, chatmessage, codelockchange, candemolish, canunlock = false;
        bool canmail = true;
        bool canmount, candismount = false;
        bool CH47attacked = true;
        bool CH47killed = true;
        bool codeenter = false;
        bool cratedrop, cratehack, cratehackend, dispenserbonus = false;
        bool doorknocked = true;
        bool dismounted, groupgrant, grouprevoke, lootitem, npcposition, onvoice, onoven = false;
        bool playerkick = true;
        bool playerban = true;
        bool playerunban = true;
        bool playerdie = false;
        bool playerdisconnect = true;
        bool playerrespawn, playerrespawned, playersleep, playersleepend, playerspawn ,playerspectate, playerspectateend, playerviolation, pluginloaded, pluginunloaded = false;
        bool playerconnect = true;
        bool lootplayer, lootentity, mounted = false;
        bool rconnection = true;
        bool rconcommand = true;
        bool serversave, shopcomplete, signupdate, servermessage, usernameupdate, usergrant, userrevoke, wounded = false;
        public string BotToken;
        public ulong ChannelID;
        //bool entitykill = false;

#endregion

        private bool ConfigChanged;
		private void Init()
        {
            LoadVariables();
            UnsubscribeMeSir();
        }

#region CONFIG

        protected override void LoadDefaultConfig()
            {
                LoadVariables();
            }

        private void LoadVariables()
        {
            Prefix = Convert.ToString(GetConfig("Chat Settings", "Prefix", "[NTD] :"));                       // CHAT PLUGIN PREFIX
            PrefixColor = Convert.ToString(GetConfig("Chat Settings", "PrefixColor", "#42d7f4"));                // CHAT PLUGIN PREFIX COLOR
            ChatColor = Convert.ToString(GetConfig("Chat Settings", "ChatColor", "#b7f5ff"));                    // CHAT  COLOR
            SteamIDIcon = Convert.ToUInt64(GetConfig("Settings", "SteamIDIcon", "76561198044414155"));        // SteamID FOR PLUGIN ICON - STEAM PROFILE CREATED FOR THIS PLUGIN / NOT YET /           
            BotToken = Convert.ToString(GetConfig("Discord Settings", "BotToken", "0"));
            ChannelID = Convert.ToUInt64(GetConfig("Discord Settings", "ChannelID", "0"));
            WebHook = Convert.ToBoolean(GetConfig("Discord Webhook", "Use WebHook via DicordMessages plugin dependency", false));
            WebHookURL = Convert.ToString(GetConfig("Discord Webhook", "URL from your Discord channel", ""));

//PLAYER CONNECT/SPAWN/SLEEP
            playerspawn = Convert.ToBoolean(GetConfig("NOTIFY -=- PLAYER -=- CONNECT/SPAWN/SLEEP", "Player Spawn", false));
            playerrespawn = Convert.ToBoolean(GetConfig("NOTIFY -=- PLAYER -=- CONNECT/SPAWN/SLEEP", "Player Respawn", false));
            playerrespawned = Convert.ToBoolean(GetConfig("NOTIFY -=- PLAYER -=- CONNECT/SPAWN/SLEEP", "Player Respawned", false));
            playersleep = Convert.ToBoolean(GetConfig("NOTIFY -=- PLAYER -=- CONNECT/SPAWN/SLEEP", "Player Sleep Started", false));
            playersleepend = Convert.ToBoolean(GetConfig("NOTIFY -=- PLAYER -=- CONNECT/SPAWN/SLEEP", "Player Sleep Ended", false));
            playerdisconnect = Convert.ToBoolean(GetConfig("NOTIFY -=- PLAYER -=- CONNECT/SPAWN/SLEEP", "Player has Disconnected", true));            //true player  connect
            playerconnect = Convert.ToBoolean(GetConfig("NOTIFY -=- PLAYER -=- CONNECT/SPAWN/SLEEP", "Player has Connected", true));             //true player disconnect
//MESSAGES
            chatmessage = Convert.ToBoolean(GetConfig("NOTIFY -=- MESSAGES -=- PLAYER/SERVER", "On Player Chat message", false));
            servermessage = Convert.ToBoolean(GetConfig("NOTIFY -=- MESSAGES -=- PLAYER/SERVER", "On Server message", false));
//ADMIN ACTION
            playerkick = Convert.ToBoolean(GetConfig("NOTIFY -=- ADMIN", "Player Kicked", true));                           //true player kick
            playerban = Convert.ToBoolean(GetConfig("NOTIFY -=- ADMIN", "Player Banned", true));                            //true player ban
            playerunban = Convert.ToBoolean(GetConfig("NOTIFY -=- ADMIN", "Player Unbanned", true));                        //true player unban
            usergrant = Convert.ToBoolean(GetConfig("NOTIFY -=- ADMIN", "User granted permission", false));
            userrevoke = Convert.ToBoolean(GetConfig("NOTIFY -=- ADMIN", "User revoked permission", false));
            groupgrant = Convert.ToBoolean(GetConfig("NOTIFY -=- ADMIN", "Group granted permission", false));
            grouprevoke = Convert.ToBoolean(GetConfig("NOTIFY -=- ADMIN", "Group revoked permission", false));
//PLAYER SPECTATE
            playerspectate = Convert.ToBoolean(GetConfig("NOTIFY -=- SPECTATE", "Player Spectate", false));
            playerspectateend = Convert.ToBoolean(GetConfig("NOTIFY -=- SPECTATE", "Player Spectate End", false));
//PLAYER HACK VIOLATION            
            playerviolation = Convert.ToBoolean(GetConfig("NOTIFY -=- HACK VIOLATION", "Player Hack Violation", false));        
//PLAYER LOOT          
            lootplayer = Convert.ToBoolean(GetConfig("NOTIFY -=- PLAYER -=- LOOT", "Player Loot Player", false));
            lootentity = Convert.ToBoolean(GetConfig("NOTIFY -=- PLAYER -=- LOOT", "Player Loot Entity", false));
            lootitem = Convert.ToBoolean(GetConfig("NOTIFY -=- PLAYER -=- LOOT", "Player is looting item", false));
//PLAYER HEALTH/KILL/DEMOLISH        
            playerdie = Convert.ToBoolean(GetConfig("NOTIFY -=- PLAYER -=- HEALTH/KILL/DEMOLISH", "Player Die", false));
            wounded = Convert.ToBoolean(GetConfig("NOTIFY -=- PLAYER -=- HEALTH/KILL/DEMOLISH", "Player wounded", false));
            //entitykill = Convert.ToBoolean(GetConfig("NOTIFY -=- PLAYER -=- HEALTH/KILL/DEMOLISH", "Killed entity", false));
            candemolish = Convert.ToBoolean(GetConfig("NOTIFY -=- PLAYER -=- HEALTH/KILL/DEMOLISH", "Player demolishing entity", false));
//PLAYER OPTIONS
            usernameupdate = Convert.ToBoolean(GetConfig("NOTIFY -=- PLAYER -=- ELSE", "Player updated name", false));
            onvoice = Convert.ToBoolean(GetConfig("NOTIFY -=- PLAYER -=- ELSE", "Player is speaking in a microphone", false));
//PLAYER /SHOP/BONUS (could be great for RolePlaying use)
            shopcomplete = Convert.ToBoolean(GetConfig("NOTIFY -=- SHOP -=- BONUS", "Player customer has complete a shop trade", false));
            addvendingoffer = Convert.ToBoolean(GetConfig("NOTIFY -=- SHOP -=- BONUS", "New sell offer added in VendingMachine", false));
            canmail = Convert.ToBoolean(GetConfig("NOTIFY -=- SHOP -=- BONUS", "a Player is accessing a mailbox", true));                   //true mailbox
            doorknocked = Convert.ToBoolean(GetConfig("NOTIFY -=- SHOP -=- BONUS", "a door has been knocked", true));                       //true knock
            onoven = Convert.ToBoolean(GetConfig("NOTIFY -=- SHOP -=- BONUS", "player start a oven", false));
            dispenserbonus = Convert.ToBoolean(GetConfig("NOTIFY -=- SHOP -=- BONUS", "player receive bonus item", false));
            signupdate = Convert.ToBoolean(GetConfig("NOTIFY -=- SHOP -=- BONUS", "player update a sign", false));
//PLAYER ACTIONS/ENTITY
            codelockchange = Convert.ToBoolean(GetConfig("NOTIFY -=- PLAYER -=- ACTIONS/ENTITY", "codelock change", false));
            canunlock = Convert.ToBoolean(GetConfig("NOTIFY -=- PLAYER -=- ACTIONS/ENTITY", "Player tries to unlock", false));
            codeenter = Convert.ToBoolean(GetConfig("PLAYER -=- ACTIONS/ENTITY", "code entered", false));
            canmount = Convert.ToBoolean(GetConfig("NOTIFY -=- PLAYER -=- ACTIONS/ENTITY", "Player is trying to mount", false));
            candismount = Convert.ToBoolean(GetConfig("NOTIFY -=- PLAYER -=- ACTIONS/ENTITY", "Player is trying to dismount", false));
            mounted = Convert.ToBoolean(GetConfig("NOTIFY -=- PLAYER -=- ACTIONS/ENTITY", "Players has mounted", false));
            dismounted = Convert.ToBoolean(GetConfig("NOTIFY -=- PLAYER -=- ACTIONS/ENTITY", "Player has dismounted", false));
// PLAYER FIGHT
            onattack = Convert.ToBoolean(GetConfig("NOTIFY -=- PLAYER -=- FIGHT", "Player is attacking", false));
//SERVEUR/PLUGINS
            pluginloaded = Convert.ToBoolean(GetConfig("NOTIFY -=- SERVER -=- PLUGIN", "Plugin Loaded", false));
            pluginunloaded = Convert.ToBoolean(GetConfig("NOTIFY -=- SERVER -=- PLUGIN", "Plugin Unloaded", false));
            serversave = Convert.ToBoolean(GetConfig("NOTIFY -=- SERVER -=- PLUGIN", "Server Save", false));
            rconcommand = Convert.ToBoolean(GetConfig("NOTIFY -=- SERVER -=- PLUGIN", "RCon Command", true));                      //true rcon com
            rconnection = Convert.ToBoolean(GetConfig("NOTIFY -=- SERVER -=- PLUGIN", "RCon Connection", true));                   //true rcon connect
//HELI/PLANE
            airdrop = Convert.ToBoolean(GetConfig("NOTIFY -=- PLANE/HELI", "airdrop", false));
            cratedrop = Convert.ToBoolean(GetConfig("NOTIFY -=- PLANE/HELI", "Hackable Crate Dropped", false));
            cratehack = Convert.ToBoolean(GetConfig("NOTIFY -=- PLANE/HELI", "Hack of Crate has started", false));
            cratehackend = Convert.ToBoolean(GetConfig("NOTIFY -=- PLANE/HELI", "Hack of Crate has ended", false));
            CH47attacked = Convert.ToBoolean(GetConfig("NOTIFY -=- PLANE/HELI", "CH47 is under attack", true));             //true CH47 attacked
            CH47killed = Convert.ToBoolean(GetConfig("NOTIFY -=- PLANE/HELI", "CH47 has been killed", true));               //true CH47 killed
//NPC / APC
            apchunt = Convert.ToBoolean(GetConfig("NOTIFY -=- BOTS -=- APC/NPC", "BRADLEY APC on hunt", false));               //true CH47 killed
//SPECIAL RAID/BIG WEAPONS
// EXTENDED
            extreceivedsnap = Convert.ToBoolean(GetConfig("NOTIFY EXTENDED -=- PLAYER -=- MISC & ROLEPLAY", "player received a snapshot", false));
            extnpcattack = Convert.ToBoolean(GetConfig("NOTIFY EXTENDED -=- BOTS -=- APC/NPC", "NPC is attacking entity", false));               
            extusesleepbag = Convert.ToBoolean(GetConfig("NOTIFY EXTENDED -=- PLAYER -=- MISC & ROLEPLAY", "NPC is attacking entity", false));               
            exthelispawned = Convert.ToBoolean(GetConfig("NOTIFY EXTENDED -=- HELI/PLANE -=- ", "Helicopter spawned", false));
            extusecar = Convert.ToBoolean(GetConfig("NOTIFY EXTENDED -=- VEHICLE -=- ", "Player using car", false));
            extchinookspawned = Convert.ToBoolean(GetConfig("NOTIFY EXTENDED -=- HELI/PLANE -=- ", "Chinook spawned", false));
// DANGEROUS TREASURES
            dangerousmessage = Convert.ToBoolean(GetConfig("NOTIFY DANGEROUS TREASURES -=- MESSAGE -=-", "DangerousTreasures message to player", false));
// HUMAN NPC
            //OVERFLOW npconenter = Convert.ToBoolean(GetConfig("NOTIFY HUMAN NPC -=- BOTS -=- ", "player approaching a HumanNPC", false));               
            //OVERFLOW npconuse = Convert.ToBoolean(GetConfig("NOTIFY HUMAN NPC -=- BOTS -=- ", "player approaching a HumanNPC", false));               
            // OVERFLOW    npcposition = Convert.ToBoolean(GetConfig("NOTIFY HUMAN NPC -=- BOTS -=- ", "HumanNPC reaches a waypoint and changes to an other", false));
// BONUS
            countonline = Convert.ToBoolean(GetConfig("NOTIFY BONUS -=- ONLINE PLAYERS REPORTER -=-", "Report online players count; refresh on players connections and every 15min", true));

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

    void Loaded()
    {
            if (HooksExtended == false) {PrintWarning("HooksExtended.cs is needed for some functions. (https://umod.org/plugins/hooks-extended)");}
            if (DangerousTreasures == false) {PrintWarning("DangerousTreasures.cs is needed for some functions. (https://umod.org/plugins/dangerous-treasures)");}
            if (HumanNPC == false) {PrintWarning("HumanNPC.cs is needed for some functions. (https://umod.org/plugins/human-npc)");}
            if (DiscordMessages == false) {PrintWarning("DiscordMessages.cs is needed for some functions. (https://umod.org/plugins/discord-messages)");}
    }

    private void OnServerInitialized()
    {
        if (countonline == true)
        {
            timer.Repeat(900, 0, () =>
            {
                var activcount = BasePlayer.activePlayerList.Count;
                if (activcount >= 1)
                {
                    string todiscord = $":family_wwgb:  --- PLAYER COUNT REPORT```css\n[ONLINE NOW] : {activcount} player(s)\n```";
                    NotifyDiscord(todiscord);  
                }
            });
        }
    }

#region MESSAGES

    protected override void LoadDefaultMessages()
    {
        lang.RegisterMessages(new Dictionary<string, string>
        {
            {"BetterChatMsg", ":lips: --- PLAYER {0} - BetterChat message```css\n{1}\n```"},
            {"ChatMsg", ":lips: --- {0} chat message```css\n{1}\n```"},
            {"OnGroupPermissionGrantedMsg", "```css\ngroup {0} - has been granted permission : {1}\n```"},
            {"OnGroupPermissionRevokedMsg", "```css\ngroup {0} - has been revoked permission : {1}\n```"},
            {"OnLootPlayerMsg", "```css\n[LOOT PvP] PLAYER {0} - STEAMID {1} - LOOTING : {2} - STEAMID {3}\n```"},
            {"OnLootEntityMsg", "```css\n[LOOT ENTITY] PLAYER {0} - STEAMID {1} - LOOTING : {2}\n```"},
            {"OnPlayerBannedMsg", ":no_pedestrians: --- BANNED PLAYER```css\nSTEAMID {0}: - PLAYER {1} - IP : {2} - REASON :{3}\n```"},
            {"OnPlayerUnbannedMsg", ":passport_control: --- UNBANNED PLAYER```css\nUNBANNED - STEAMID {0}: - PLAYER {1} - IP : {2}\n```"},
            {"OnPlayerDieMsg", ":skull_crossbones: --- PLAYERDIE```css\nPLAYER {0} - STEAMID {1} - INFO : {2}\n```"},
            {"OnPlayerDisconnectedMsg", ":closed_book:  --- PLAYER DISCONNECTED```css\nPLAYER {0} - STEAMID {1} - disconnected - REASON : {2}\n```"},
            {"OnPlayerRespawnMsg", "```css\n[RESPAWN] - PLAYER {0} - STEAMID {1} - respawn\n```"},
            {"OnPlayerRespawnedMsg", "```css\n[RESPAWNED] - PLAYER {0} - STEAMID {1} - respawned\n```"},
            {"OnPlayerSleepMsg", ":zzz: --- PLAYER {0} - STEAMID {1} - is falling asleep"},
            {"OnPlayerSleepEndedMsg", "```css\n[RESPAWNED] - PLAYER {0} - STEAMID {1} - respawned\n```"},
            {"OnPlayerSpawnMsg", "```css\n[SPAWN] - PLAYER {0} - STEAMID {1} - spawn\n```"},
            {"OnPlayerSpectateMsg", ":eye: --- PLAYER {0} - STEAMID {1} - spectate start{2}"},
            {"OnPlayerSpectateEndMsg", ":eye: --- PLAYER {0} - STEAMID {1} - spectate end {2}"},
            {"OnPlayerViolationMsg", ":no_entry: --- HACK VIOLATION```css\nPLAYER {0} - STEAMID {1} - violation hack : {2}\n```"},
            {"OnPlayerInitMsg", ":green_book: --- PLAYER CONNECTED```css\n{0} - STEAMID {1} - has connected\n```"},
            {"OnPlayerKickPlayerKickedMsg", ":hockey: --- KICKED PLAYER```css\nSTEAMID {0}: - PLAYER {1} - REASON :{2}\n```"},
            {"OnPluginLoadedMsg", ":cyclone:   --- {0} - loaded"},
            {"OnPluginUnloadedMsg", ":cyclone:   --- {0} - unloaded"},
            {"OnServerSaveMsg", ":floppy_disk: --- SERVER IS SAVING."},
            {"OnServerMessageMsg", ":speech_balloon: --- {0}"},
            {"OnUserNameUpdatedMsg", "```css\nPLAYER : {0} - SteamID {1} - updated name to PLAYER : {2}\n```"},
            {"CanChangeCodeMsg", "```css\n{0} - STEAMID {1} - tries to changecodelock {2} - NEW CODE {3} - IS GUEST CODE : {4}\n```"},
            {"CanUnlockMsg", ":unlock: --- PLAYER {0} - STEAMID {1} - is trying to unlock"},
            {"OnCodeEnteredMsg", ":key: --- CODE ENTERED```css\nPLAYER {0} - SteamID {1} - has entered CODELOCK : {2}\n```"},
            {"CanBeWoundedMsg", "```css\n[WOUND TRY] - PLAYER {0} - STEAMID {1} - {2}\n```"},
            {"OnUserPermissionGrantedMsg", "```css\n[PERMISSION GRANTED] - STEAMID {0} - as been granted permission {1}\n```"},
            {"OnUserPermissionRevokedMsg", "```css\n[PERMISSION REVOKED] - STEAMID {0} - as been revoked permission {1}\n```"},
            {"CanMountEntityMsg", "```css\n[MOUNT TRY] - PLAYER {0} - STEAMID {1} - {2}\n```"},
            {"OnEntityMountedMsg", "```css\n[MOUNTED] - PLAYER {0} - STEAMID {1} - {2}\n```"},
            {"CanDismountEntity", "```css\n[DISMOUNT TRY] - PLAYER {0} - STEAMID {1} - {2}\n```"},
            {"OnEntityDismountedMsg", "```css\n[DISMOUNTED] - PLAYER {0} - STEAMID {1} - {2}\n```"},
            {"OnLootItemMsg", "```css\n[LOOT] - PLAYER {0} - STEAMID {1} - {2}\n```"},
            {"OnRconCommandMsg", "```css\n[RCON] command - IP : {0} - {1} - {2}\n```"},
            {"OnRconConnectionMsg", "```css\n[RCON] connected to - IP {0}\n```"},
            {"OnAddVendingOfferMsg", "```css\n[VENDINGMACHINE] {0} - new SellOrder added : {1}\n```"},
            {"OnShopCompleteTradeMsg", ":shopping_cart: --- PLAYER CUSTOMER : {0} - STEAMID {1} has complete a trade"},
            {"OnAirdropMsg", ":airplane: --- AIRDROP```css\nAIRDROP is on its way - PLANE : {0} - POSITION : {1}\n```"},
            {"OnCrateDroppedMsg", ":kaaba: --- Hackable Locked Crate Dropped"},
            {"OnCrateHackMsg", ":kaaba: --- Crate Hack has started"},
            {"OnCrateHackEndMsg", ":kaaba: --- Crate Hack has ended"},
            {"OnHelicopterAttackedMsg", ":helicopter: --- CH47 is under attack - HELI : {0}"},
            {"OnHelicopterKilledMsg", ":helicopter: --- CH47 has been killed - HELI : {0}"},
            {"OnBradleyApcHuntMsg", ":robot:  --- BRADLEY APC ON HUNT : {0}"},
            {"OnDoorKnockedMsg", ":door: --- PLAYER  {0} - has knocked here : {1}"},
            {"CanUseMailboxMsg", ":mailbox_closed: --- PLAYER {0} - is trying to acces mailbox {1}"},
            {"OnOvenToggleMsg", ":fire: --- [OVEN START] PLAYER {0}"},
            {"OnDispenserBonusMsg", ":trophy:  --- [BONUS] {0} - PLAYER {1} - ITEM {2}"},
            {"OnSignUpdatedMsg", ":frame_photo: --- [SIGN] {0} - PLAYER {1} - TEXT {2}"},
            {"OnPlayerAttackMsg", ":gun: --- PLAYER {0} launch an attack ! - INFO : {1}"},
            {"OnPlayerVoiceMsg", ":microphone2: --- PLAYER {0} is voice speaking !!!"},
            {"CanDemolishMsg", "ICONE --- [DEMOLISHING] - PLAYER {0} - {1} - OWNER IS {2}"},
            {"OnReceivedSnapshotMsg", ":camera_with_flash: --- [SNAPSHOT] received by - PLAYER {0}"},
            {"OnNPCAttackMsg", ":space_invader: --- [NPC] {0} - IS ATTACKING {1} - INFO {2}"},
            {"OnUseSleepingBagMsg", ":sleeping_accommodation: --- [SLEEPING BAG] {0} - PLAYER {1}"},
            {"OnHelicopterSpawnedMsg", ":helicopter: --- [HELICOPTER] {0}"},
            {"OnChinookSpawnedMsg", ":helicopter: --- [CHINOOK] SPAWNED {0}"},
            {"OnUseCarMsg", ":red_car: --- [CAR] {0} USED by - PLAYER {1}"},
            {"OnDangerousMessageMsg", ":volcano: --- ```html\n[DANGEROUS TRASURES] {0} - PLAYER {1}\n```"},

        }, this, "en");
    }

#endregion

#region UNSUBSCRIBE
        void UnsubscribeMeSir()
        {
            if (!playerspawn) Unsubscribe(nameof(OnPlayerSpawn));
            if (!playerrespawned) Unsubscribe(nameof(OnPlayerRespawned));
            if (!playersleep) Unsubscribe(nameof(OnPlayerSleep));
            if (!playersleepend) Unsubscribe(nameof(OnPlayerSleepEnded));
            if (!playerdisconnect) Unsubscribe(nameof(OnPlayerDisconnected));
            if (!playerconnect) Unsubscribe(nameof(OnPlayerInit));
            //MESSAGES
            if (!chatmessage) Unsubscribe(nameof(OnPlayerChat));
            if (!BetterChat) Unsubscribe(nameof(OnBetterChat));
            if (!servermessage) Unsubscribe(nameof(OnServerMessage));
            //ADMIN ACTION
            if (!playerkick) Unsubscribe(nameof(OnPlayerKickPlayerKicked));
            if (!playerban) Unsubscribe(nameof(OnPlayerBanned));
            if (!playerunban) Unsubscribe(nameof(OnPlayerUnbanned));
            if (!usergrant) Unsubscribe(nameof(OnUserPermissionGranted));
            if (!userrevoke) Unsubscribe(nameof(OnUserPermissionRevoked));
            if (!groupgrant) Unsubscribe(nameof(OnGroupPermissionGranted));
            if (!grouprevoke) Unsubscribe(nameof(OnGroupPermissionRevoked));
            //PLAYER SPECTATE
            if (!playerspectate) Unsubscribe(nameof(OnPlayerSpectate));
            if (!playerspectateend) Unsubscribe(nameof(OnPlayerSpectateEnd));
            //PLAYER HACK VIOLATION            
            if (!playerviolation) Unsubscribe(nameof(OnPlayerViolation));
            //PLAYER LOOT 
            if (!lootplayer) Unsubscribe(nameof(OnLootPlayer));
            if (!lootentity) Unsubscribe(nameof(OnLootEntity));
            if (!lootitem) Unsubscribe(nameof(OnLootItem));
            //PLAYER HEALTH/KILL/DEMOLISH        
            if (!playerdie) Unsubscribe(nameof(OnPlayerDie));
            if (!wounded) Unsubscribe(nameof(CanBeWounded));
            //if (!entitykill) Unsubscribe(nameof(CanBeWounded));
            if (!candemolish) Unsubscribe(nameof(CanDemolish));
            //PLAYER OPTIONS
            if (!usernameupdate) Unsubscribe(nameof(OnUserNameUpdated));
            if (!onvoice) Unsubscribe(nameof(OnPlayerVoice));
            //PLAYER /SHOP/BONUS (could be great for RolePlaying use)
            if (!shopcomplete) Unsubscribe(nameof(OnShopCompleteTrade));
            if (!addvendingoffer) Unsubscribe(nameof(OnAddVendingOffer));
            if (!canmail) Unsubscribe(nameof(CanUseMailbox));
            if (!doorknocked) Unsubscribe(nameof(OnDoorKnocked));
            if (!onoven) Unsubscribe(nameof(OnOvenToggle));
            if (!dispenserbonus) Unsubscribe(nameof(OnDispenserBonus));
            if (!signupdate) Unsubscribe(nameof(OnSignUpdated));
            //PLAYER ACTIONS/ENTITY
            if (!codelockchange) Unsubscribe(nameof(CanChangeCode));
            if (!canunlock) Unsubscribe(nameof(CanUnlock));
            if (!codeenter) Unsubscribe(nameof(OnCodeEntered));
            if (!canmount) Unsubscribe(nameof(CanMountEntity));
            if (!candismount) Unsubscribe(nameof(CanDismountEntity));
            if (!mounted) Unsubscribe(nameof(OnEntityMounted));
            if (!dismounted) Unsubscribe(nameof(OnEntityDismounted));
            // PLAYER FIGHT
            if (!onattack) Unsubscribe(nameof(OnPlayerAttack));
            //SERVEUR/PLUGINS
            if (!pluginloaded) Unsubscribe(nameof(OnPluginLoaded));
            if (!pluginunloaded) Unsubscribe(nameof(OnPluginUnloaded));
            if (!serversave) Unsubscribe(nameof(OnServerSave));
            if (!rconcommand) Unsubscribe(nameof(OnRconCommand));
            if (!rconnection) Unsubscribe(nameof(OnRconConnection));
            //HELI/PLANE
            if (!airdrop) Unsubscribe(nameof(OnAirdrop));
            if (!cratedrop) Unsubscribe(nameof(OnCrateDropped));
            if (!cratehack) Unsubscribe(nameof(OnCrateHack));
            if (!cratehackend) Unsubscribe(nameof(OnCrateHackEnd));
            if (!CH47attacked) Unsubscribe(nameof(OnHelicopterAttacked));
            if (!CH47killed) Unsubscribe(nameof(OnHelicopterKilled));
            //NPC / APC
            if (!apchunt) Unsubscribe(nameof(OnBradleyApcHunt));
            //SPECIAL RAID/BIG WEAPONS
            // EXTENDED
            if (!extreceivedsnap) Unsubscribe(nameof(OnReceivedSnapshot));
            if (!extnpcattack) Unsubscribe(nameof(OnNPCAttack));
            if (!extusesleepbag) Unsubscribe(nameof(OnUseSleepingBag));
            if (!exthelispawned) Unsubscribe(nameof(OnHelicopterSpawned));
            if (!extusecar) Unsubscribe(nameof(OnUseCar));
            if (!extchinookspawned) Unsubscribe(nameof(OnChinookSpawned));
            // DANGEROUS TREASURES
            if (!dangerousmessage) Unsubscribe(nameof(OnDangerousMessage));
        }
#endregion
        
#region WEBHOOK

void SendWithWebHook(string MessageText)
{
    if (String.IsNullOrEmpty(WebHookURL))
    {
        PrintWarning("Please set a WebHookURL");
        return;
    }
    DiscordMessages?.Call("API_SendTextMessage", WebHookURL, MessageText);
}

#endregion

#region DISCORD MESSAGER
        void SendMessage(string MessageText)
        {
            if (WebHook)
            {
                SendWithWebHook(MessageText);
                return;
            }

            string payloadJson = JsonConvert.SerializeObject(new DiscordPayload()
            {
                MessageText = MessageText
            });
            Dictionary<string, string> headers = new Dictionary<string, string>();
            if(BotToken.StartsWith("Bot "))
            {
                headers.Add("Authorization", BotToken);
            }
            else
            {
                headers.Add("Authorization", String.Format("Bot {0}", BotToken));
            }
            headers.Add("Content-Type", "application/json");

            string url = BaseURLTemplate.Replace("{{ChannelID}}", $"{ChannelID.ToString()}");
            webrequest.EnqueuePost(url, payloadJson, (code, response) => PostCallBack(code, response), this, headers);
        }

        void PostCallBack(int code, string response)
        {
            if(code != 200) PrintWarning(String.Format("Discord Api responded with {0}: {1}", code, response));
        }

        void NotifyDiscord(string todiscord)
        {
            if (queue.Contains(todiscord) == true)
            {
                if (debug) {Puts($"this message is already in the recent queue. sending cancelled.");}
                return;
            }
            queue.Add(todiscord);
            float count = queue.Count;
            if (discording == true)
            {
                count = count + 2;
                float format = count;
                if (debug == true) {Puts($"message will be send in {count} seconds");}
                timer.Once(count, () =>
                {
                    string time = DateTime.Now.ToShortTimeString();
                    string todisplay = $"{time} {todiscord}"; 
                    SendMessage(todisplay);
                    queue.Remove(todiscord);
                });
            }
            if (discording == false)
            {
                string time = DateTime.Now.ToShortTimeString();
                string todisplay = $"{time} {todiscord}"; 
                SendMessage(todisplay);
                queue.Remove(todiscord);
                discording = true;
                if (debug) {Puts($"a message has gone, discording is now -true");}
                timer.Once(2f, () =>
                {
                    discording = false;
                    if (debug) {Puts($"discording is now -false");}
                });
            }
        }

        class DiscordPayload
        {
            [JsonProperty("content")]
            public string MessageText { get; set; }
        }
#endregion

#region ON BETTERCHAT
        void OnBetterChat(Dictionary<string, object> dict)
        {
            if (chatmessage == false)
            {
                if (debug) {Puts($"BetterChat message IGNORED");}return;
            }
            var txt = (dict["Text"]);
            var iplayer = dict["Player"] as IPlayer;
            if (iplayer == null) return;
            var message = BetterChat.Call ("API_GetFormattedMessage", iplayer, txt, true);
            //var name = BetterChat?.Call ("API_GetFormattedUsername", iplayer);
            //string todiscord = $":lips: --- PLAYER {iplayer.Name} - BetterChat message```css\n{message}\n```";
            string todiscord = String.Format(lang.GetMessage("BetterChatMsg", this, null),iplayer.Name,message);
            NotifyDiscord(todiscord);   
        }
#endregion
#region ON PLAYER CHAT

        void OnPlayerChat(ConsoleSystem.Arg arg)
        {
            if (BetterChat == false)
            {
                if (chatmessage == false)
                {
                    if (debug) {Puts($"Chat message IGNORED");} return;
                }
                var player = arg.Connection.player as BasePlayer;
                string playername;
                playername = player.displayName;
                if (player.displayName.Length > 24) {playername = playername.Substring(0,24);}
                string[] blabla;
                string message;
                blabla = arg.Args.ToArray();
                message = blabla[0];
                string messagelowered = message.ToLower();
                string todiscord = String.Format(lang.GetMessage("ChatMsg", this, null),playername,message);
                NotifyDiscord(todiscord);   
            }
        }

#endregion
        string playername;
        string GetPlayerName(BasePlayer player)
        {
            if (player == null){playername = "Unknown Player";}
            foreach (BasePlayer playeractiv in BasePlayer.activePlayerList.ToList())
                {
                    if (player == playeractiv)
                    {
                        playername = player.displayName;
                        if (player.displayName.Length > 24) {playername = playername.Substring(0,24);}
                        return(playername);
                    }
                }            
            return "Unknown";
        }

// *************************************************************************************************** HOOKS ****************

//************************************** PERMISSIONS *************** PERMISSIONS **************************************/

        void OnGroupPermissionGranted(string name, string perm)
        {
            string todiscord = String.Format(lang.GetMessage("OnGroupPermissionGrantedMsg", this, null),name,perm); NotifyDiscord(todiscord);
        }

        void OnGroupPermissionRevoked(string name, string perm)
        {
            string todiscord = String.Format(lang.GetMessage("OnGroupPermissionRevokedMsg", this, null),name,perm); NotifyDiscord(todiscord);
        }

        void OnLootPlayer(BasePlayer player, BasePlayer target)
        {
            string name = GetPlayerName(player); string targeted = GetPlayerName(target); string todiscord = String.Format(lang.GetMessage("OnLootPlayerMsg", this, null),name,player.UserIDString,targeted,target.UserIDString); NotifyDiscord(todiscord);
        }

        void OnLootEntity(BasePlayer player, BaseEntity entity)
        {
            string name = GetPlayerName(player); string todiscord = String.Format(lang.GetMessage("OnLootEntityMsg", this, null),name,player.UserIDString,entity.ToString()); NotifyDiscord(todiscord); 
        }

        void OnPlayerBanned(string name, ulong id, string address, string reason)
        {
            string todiscord = String.Format(lang.GetMessage("OnPlayerBannedMsg", this, null),id.ToString(),name,address,reason); NotifyDiscord(todiscord); 
        }

        void OnPlayerUnbanned(string name, ulong id, string address)
        {
            string todiscord = String.Format(lang.GetMessage("OnPlayerUnbannedMsg", this, null),id.ToString(),name,address); NotifyDiscord(todiscord);         
        }

        void OnPlayerDie(BasePlayer player, HitInfo info)
        {
            string name = GetPlayerName(player);
            string todiscord = String.Format(lang.GetMessage("OnPlayerDieMsg", this, null),name,player.UserIDString,info); NotifyDiscord(todiscord);   
        }

        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            string name = GetPlayerName(player); string todiscord = String.Format(lang.GetMessage("OnPlayerDisconnectedMsg", this, null),name,player.UserIDString,reason); NotifyDiscord(todiscord);  
        }

        void OnPlayerRespawn(BasePlayer player)
        {
            string name = GetPlayerName(player); string todiscord = String.Format(lang.GetMessage("OnPlayerRespawnMsg", this, null),name,player.UserIDString); NotifyDiscord(todiscord);  
        }

        void OnPlayerRespawned(BasePlayer player)
        {
            string name = GetPlayerName(player); string todiscord = String.Format(lang.GetMessage("OnPlayerRespawnedMsg", this, null),name,player.UserIDString); NotifyDiscord(todiscord);  
        }

        void OnPlayerSleep(BasePlayer player)
        {
            string name = GetPlayerName(player); string todiscord = String.Format(lang.GetMessage("OnPlayerSleepMsg", this, null),name,player.UserIDString); NotifyDiscord(todiscord);  
        }

        void OnPlayerSleepEnded(BasePlayer player)
        {
            string name = GetPlayerName(player); string todiscord = String.Format(lang.GetMessage("OnPlayerSleepEndedMsg", this, null),name,player.UserIDString); NotifyDiscord(todiscord);  
        }

        void OnPlayerSpawn(BasePlayer player)
        {
            string name = GetPlayerName(player); string todiscord = String.Format(lang.GetMessage("OnPlayerSpawnMsg", this, null),name,player.UserIDString); NotifyDiscord(todiscord);  
        }

        void OnPlayerSpectate(BasePlayer player, string spectateFilter)
        {
            string name = GetPlayerName(player); string todiscord = String.Format(lang.GetMessage("OnPlayerSpectateMsg", this, null),name,player.UserIDString,spectateFilter); NotifyDiscord(todiscord);  
        }

        void OnPlayerSpectateEnd(BasePlayer player, string spectateFilter)
        {
            string name = GetPlayerName(player); string todiscord = String.Format(lang.GetMessage("OnPlayerSpectateEndMsg", this, null),name,player.UserIDString,spectateFilter); NotifyDiscord(todiscord);  
        }

//***************************** ADMIN ****************** ADMIN ************************** ADMIN ************************/

        void OnPlayerViolation(BasePlayer player, AntiHackType type, float amount)
        {
            string name = GetPlayerName(player); string todiscord = String.Format(lang.GetMessage("OnPlayerViolationMsg", this, null),name,player.UserIDString,type.ToString()); NotifyDiscord(todiscord);  
        }

        void OnPlayerInit(BasePlayer player)
        {
            string name = GetPlayerName(player); string todiscord = String.Format(lang.GetMessage("OnPlayerInitMsg", this, null),name,player.UserIDString); NotifyDiscord(todiscord); 
        }

        void OnPlayerKickPlayerKicked(BasePlayer player, string reason)
        {
            string name = GetPlayerName(player); string todiscord = String.Format(lang.GetMessage("OnPlayerKickPlayerKickedMsg", this, null),player.UserIDString,name,reason); NotifyDiscord(todiscord); 
        }

        void OnPluginLoaded(Plugin name)
        {
            string todiscord = String.Format(lang.GetMessage("OnPluginLoadedMsg", this, null),name); NotifyDiscord(todiscord); 
        }

        void OnPluginUnloaded(Plugin name)
        {
            string todiscord = String.Format(lang.GetMessage("OnPluginUnloadedMsg", this, null),name); NotifyDiscord(todiscord); 
        }

//***************************** MESSAGES ************** MESSAGES ************************* MESSAGES *************/

        void OnServerSave()
        {
            string todiscord = lang.GetMessage("OnServerSaveMsg", this, null); NotifyDiscord(todiscord); 
        }

        void OnServerMessage(string message, string name, string color, ulong id)
        {
            string todiscord = String.Format(lang.GetMessage("OnServerMessageMsg", this, null),message); NotifyDiscord(todiscord); 
        }

        void OnUserNameUpdated(string id, string oldName, string newName)
        {
            string todiscord = String.Format(lang.GetMessage("OnUserNameUpdatedMsg", this, null),oldName,id,newName); NotifyDiscord(todiscord); 
        }

//***************************** LOCK *********************/

        void CanChangeCode(CodeLock codeLock, BasePlayer player, string newCode, bool isGuestCode)
        {
            string name = GetPlayerName(player); string todiscord = String.Format(lang.GetMessage("CanChangeCodeMsg", this, null),name,player.UserIDString,codeLock.code,newCode,isGuestCode.ToString()); NotifyDiscord(todiscord); 
        }

        void CanUnlock(BasePlayer player, BaseLock baseLock)
        {
            string name = GetPlayerName(player);
            string todiscord = String.Format(lang.GetMessage("CanUnlockMsg", this, null),name,player.UserIDString); NotifyDiscord(todiscord); 
        }

        void OnCodeEntered(CodeLock codeLock, BasePlayer player, string code)
        {
            string name = GetPlayerName(player);
            string todiscord = String.Format(lang.GetMessage("OnCodeEnteredMsg", this, null),name,player.UserIDString,code);NotifyDiscord(todiscord); 
        }

        void CanBeWounded(BasePlayer player, HitInfo info)
        {
            string name = GetPlayerName(player);
            string todiscord = String.Format(lang.GetMessage("CanBeWoundedMsg", this, null),name,player.UserIDString,info); NotifyDiscord(todiscord); 
        }

//***************************** PERMISSION *********************/

        void OnUserPermissionGranted(string id, string perm)
        {
            string todiscord = String.Format(lang.GetMessage("OnUserPermissionGrantedMsg", this, null),id,perm); NotifyDiscord(todiscord); 
        }

        void OnUserPermissionRevoked(string id, string perm)
        {
            string todiscord = String.Format(lang.GetMessage("OnUserPermissionRevokedMsg", this, null),id,perm); NotifyDiscord(todiscord); 
        }

//*****************************  MOUNT DISMOUNT *********************/

        void CanMountEntity(BasePlayer player, BaseMountable entity)
        {
            string name = GetPlayerName(player);
            string todiscord = String.Format(lang.GetMessage("CanMountEntityMsg", this, null),name,player.UserIDString,entity.ToString()); NotifyDiscord(todiscord); 
        }

        void OnEntityMounted(BaseMountable entity, BasePlayer player)
        {
            string name = GetPlayerName(player);
            string todiscord = String.Format(lang.GetMessage("OnEntityMountedMsg", this, null),name,player.UserIDString,entity.ToString()); NotifyDiscord(todiscord); 
        }

        void CanDismountEntity(BasePlayer player, BaseMountable entity)
        {
            string name = GetPlayerName(player);
            string todiscord = String.Format(lang.GetMessage("CanDismountEntityMsg", this, null),name,player.UserIDString,entity.ToString()); NotifyDiscord(todiscord); 
        }

        void OnEntityDismounted(BaseMountable entity, BasePlayer player)
        {
            string name = GetPlayerName(player);
            string todiscord = String.Format(lang.GetMessage("OnEntityDismountedMsg", this, null),name,player.UserIDString,entity.ToString()); NotifyDiscord(todiscord); 
        }
//***************************** LOOT *********************/

        void OnLootItem(BasePlayer player, Item item)
        {
            string name = GetPlayerName(player);
            string todiscord = String.Format(lang.GetMessage("OnLootItemMsg", this, null),name,player.UserIDString,item.ToString()); NotifyDiscord(todiscord); 
        }

//***************************** RCON *********************/

        void OnRconCommand(IPAddress ip, string command, string[] args)
        {
            string todiscord = String.Format(lang.GetMessage("OnRconCommandMsg", this, null),ip.ToString(),command,args.ToString()); NotifyDiscord(todiscord); 
        }

        void OnRconConnection(IPEndPoint ip)
        {
            string todiscord = String.Format(lang.GetMessage("OnRconConnectionMsg", this, null),ip.ToString()); NotifyDiscord(todiscord); 
        }

//***************************** VENDING MACHINE & SHOP *********************/

        void OnAddVendingOffer(VendingMachine machine, ProtoBuf.VendingMachine.SellOrder sellOrder)
        {
            string todiscord = String.Format(lang.GetMessage("OnAddVendingOfferMsg", this, null),machine.ToString(),sellOrder.ToString()); NotifyDiscord(todiscord);
        }

        void OnShopCompleteTrade(ShopFront shop, BasePlayer customer)
        {
            string name = GetPlayerName(customer);
            string todiscord = String.Format(lang.GetMessage("OnShopCompleteTradeMsg", this, null),name,customer.UserIDString); NotifyDiscord(todiscord); 
        }
        
//***************************** PLANE / HELI *********************/

        void OnAirdrop(CargoPlane plane, Vector3 dropPosition)
        {
            string todiscord = String.Format(lang.GetMessage("OnAirdropMsg", this, null),plane.ToString(),dropPosition.ToString()); NotifyDiscord(todiscord); 
        }

        void OnCrateDropped(HackableLockedCrate crate)
        {
            string todiscord = lang.GetMessage("OnCrateDroppedMsg", this, null); NotifyDiscord(todiscord); 
        }

        void OnCrateHack(HackableLockedCrate crate)
        {
            string todiscord = lang.GetMessage("OnCrateHackMsg", this, null); NotifyDiscord(todiscord); 
        }

        void OnCrateHackEnd(HackableLockedCrate crate)
        {
            string todiscord = lang.GetMessage("OnCrateHackEndMsg", this, null); NotifyDiscord(todiscord); 
        }
        void OnHelicopterAttacked(CH47HelicopterAIController heli)
        {
            string todiscord = String.Format(lang.GetMessage("OnHelicopterAttackedMsg", this, null),heli.ToString()); NotifyDiscord(todiscord);
        }

        void OnHelicopterKilled(CH47HelicopterAIController heli)
        {
            string todiscord = String.Format(lang.GetMessage("OnHelicopterKilledMsg", this, null),heli.ToString()); NotifyDiscord(todiscord);
        }

        void OnBradleyApcHunt(BradleyAPC apc)
        {
            string todiscord = String.Format(lang.GetMessage("OnBradleyApcHuntMsg", this, null),apc.ToString()); NotifyDiscord(todiscord);
        }

//***************************** ROLEPPLAY USE *********************/

        void OnDoorKnocked(Door door, BasePlayer player)
        {
            string name = GetPlayerName(player);
            string todiscord = String.Format(lang.GetMessage("OnDoorKnockedMsg", this, null),name,door.ToString()); NotifyDiscord(todiscord);
        }

        void CanUseMailbox(BasePlayer player, Mailbox mailbox)
        {
            string name = GetPlayerName(player);
            string todiscord = String.Format(lang.GetMessage("CanUseMailboxMsg", this, null),name,mailbox.ToString()); NotifyDiscord(todiscord);
        }

        void OnOvenToggle(BaseOven oven, BasePlayer player)
        {
            string name = GetPlayerName(player);
            string todiscord = String.Format(lang.GetMessage("OnOvenToggleMsg", this, null),name); NotifyDiscord(todiscord);
        }

        void OnDispenserBonus(ResourceDispenser dispenser, BasePlayer player, Item item)
        {
            string name = GetPlayerName(player);
            string todiscord = String.Format(lang.GetMessage("OnDispenserBonusMsg", this, null),dispenser,name,item); NotifyDiscord(todiscord);
        }

        void OnSignUpdated(Signage sign, BasePlayer player, string text)
        {
            string name = GetPlayerName(player);
            string todiscord = String.Format(lang.GetMessage("OnSignUpdatedMsg", this, null),sign,name,text); NotifyDiscord(todiscord);
        }

        void OnPlayerAttack(BasePlayer attacker, HitInfo info)
        {
            string name = GetPlayerName(attacker);
            string todiscord = String.Format(lang.GetMessage("OnPlayerAttackMsg", this, null),name,info.ToString()); NotifyDiscord(todiscord);
        }

        void OnPlayerVoice(BasePlayer player, Byte[] data)
        {
            string name = GetPlayerName(player);
            string todiscord = String.Format(lang.GetMessage("OnPlayerVoiceMsg", this, null),name); NotifyDiscord(todiscord);
        }
/*
void OnEntityKill(BaseNetworkable entity) //forcemment trop trop trop de choses, a voir a filtrer
        {
            if (entitykill == false) {if (debug == true) {Puts($"on entity kill IGNORED");} return;}    
            string todiscord = $"```css\n[KILL] - INFO : {entity.ToString()}```"; NotifyDiscord(todiscord);
        }
*/


/*
        void OnPlayerHealthChange(BasePlayer player, float oldValue, float newValue)
        {
    Puts("OnPlayerHealthChange works!");
    return null;
        }
*/


// RAID ORIENTED or not for moment

        void CanDemolish(BasePlayer player, BuildingBlock block, BuildingGrade.Enum grade)
        {
            string name = GetPlayerName(player);
            string todiscord = String.Format(lang.GetMessage("CanDemolishMsg", this, null),name,block,block.OwnerID); NotifyDiscord(todiscord);
        }

//********************************** HOOKS EXTENDED ******************************** */*/

        bool extreceivedsnap = false;
        bool extnpcattack = false;
        bool extusesleepbag = false;
        bool exthelispawned = false;
        bool extusecar = false;
        bool extchinookspawned = false;

        void OnReceivedSnapshot (BasePlayer player)
        {
            string name = GetPlayerName(player);
            string todiscord = String.Format(lang.GetMessage("OnReceivedSnapshotMsg", this, null),name); NotifyDiscord(todiscord);
        }

        void OnNPCAttack (BaseCombatEntity entity, NPCPlayer npc, HitInfo info)
        {
            string todiscord = String.Format(lang.GetMessage("OnNPCAttackMsg", this, null),npc,entity,info); NotifyDiscord(todiscord);
        }   

        void OnUseSleepingBag (BasePlayer source, SleepingBag bag)
        {
            string name = GetPlayerName(source);
            string todiscord = String.Format(lang.GetMessage("OnUseSleepingBagMsg", this, null),bag,name); NotifyDiscord(todiscord);
        }

        void OnHelicopterSpawned (BaseHelicopter helicopter)
        {
            string todiscord = String.Format(lang.GetMessage("OnHelicopterSpawnedMsg", this, null),helicopter); NotifyDiscord(todiscord);
        }

        void OnChinookSpawned (CH47Helicopter helicopter)
        {
            string todiscord = String.Format(lang.GetMessage("OnChinookSpawnedMsg", this, null),helicopter); NotifyDiscord(todiscord);
        }

        void OnUseCar (BasePlayer source, BaseCar target)
        {
            string todiscord = String.Format(lang.GetMessage("OnUseCarMsg", this, null),target,source); NotifyDiscord(todiscord);
        }

/********************************* DANEGROUS TREASURES *********** */

        bool dangerousmessage = false;

        void OnDangerousMessage(BasePlayer player, Vector3 eventPos, string message)
        {
            string todiscord = String.Format(lang.GetMessage("OnDangerousMessageMsg", this, null),message,player); NotifyDiscord(todiscord);
        }
    }
}

