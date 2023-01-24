using UnityEngine;
using System.Collections.Generic;
using Oxide.Core;
using Convert = System.Convert;
using System.Linq;
using Oxide.Game.Rust.Cui;

namespace Oxide.Plugins
{
    [Info("My Mini Copter", "BuzZ[PHOQUE]", "0.0.3")]
    [Description("Spawn a Mini Helicopter")]

/*======================================================================================================================= 
*   THANKS TO THE OXIDE/UMOD TEAM for coding quality, ideas, and time spent for the community
*
*   08 february 2019
*   chat commands : /mymini    /nomini
*
*   0.0.1   20190208    creation
*
*   0.0.3               console commands spawnminicopter .userID / killminicopter .userID
*=======================================================================================================================*/

    public class MyMiniCopter : RustPlugin
    {
        bool debug = false;

        string Prefix = "[My MiniCopter] :";
        ulong SteamIDIcon = 76561198059533272;

        const string prefab = "assets/content/vehicles/minicopter/minicopter.entity.prefab";

        private bool ConfigChanged;
        const string MinicopterSpawn = "myminicopter.spawn"; 
        const string MinicopterCooldown = "myminicopter.cooldown"; 


        float cooldownmin = 60f;
        float trigger = 60f;
		private Timer clock;
//BaseHelicopterVehicle

        public Dictionary<BasePlayer, BaseVehicle > baseplayerminicop = new Dictionary<BasePlayer, BaseVehicle>(); //for FUTURE

    class StoredData
    {
        public Dictionary<ulong, uint> playerminiID = new Dictionary<ulong, uint>();
        public Dictionary<ulong, float> playercounter = new Dictionary<ulong, float>();
        public StoredData()
        {
        }
    }
        private StoredData storedData;

        void Init()
        {
            LoadVariables();
            permission.RegisterPermission(MinicopterSpawn, this);
            permission.RegisterPermission(MinicopterCooldown, this);
            storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Name); 
        }

    void OnServerInitialized()
    {
        float cooldownsec = (cooldownmin * 60);
        if (cooldownsec <= 120)
        {
            PrintError("Please set a longer cooldown time. Minimum is 2 min.");
            return;
        }
		clock = timer.Repeat(trigger, 0, () =>
		{
            LetsClock();
		});
    }

        void Unload()
        {
            Interface.Oxide.DataFileSystem.WriteObject(Name, storedData);
        }

#region MESSAGES

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"AlreadyMsg", "You already have a mini helicopter.\nuse command '/nomini' to remove it."},
                {"SpawnedMsg", "Your mini copter has spawned !\nuse command '/nomini' to remove it."},
                {"KilledMsg", "Your mini copter has been removed/killed."},
                {"NoPermMsg", "You are not allowed to do this."},
                {"CooldownMsg", "You must wait before a new mini copter"},

            }, this, "en");

            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"AlreadyMsg", "Vous avez déjà un mini hélicoptère\nutilisez la commande '/nomini' pour le supprimer."},
                {"SpawnedMsg", "Votre mini hélico est arrivé !\nutilisez la commande '/nomini' pour le supprimer."},
                {"KilledMsg", "Votre mini hélico a disparu du monde."},
                {"NoPermMsg", "Vous n'êtes pas autorisé."},
                {"CooldownMsg", "Vous devez attendre avant de redemander un mini hélico"},

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
            Prefix = Convert.ToString(GetConfig("Chat Settings", "Prefix", "[My MiniCopter] :"));                       // CHAT PLUGIN PREFIX
            SteamIDIcon = Convert.ToUInt64(GetConfig("Chat Settings", "SteamIDIcon", "76561198059533272"));        // SteamID FOR PLUGIN ICON - STEAM PROFILE CREATED FOR THIS PLUGIN / NONE YET /
            cooldownmin = Convert.ToSingle(GetConfig("Cooldown (on permission)", "Value in minutes", "60"));      

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

        void LetsClock()
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList.ToList())
            {
                float cooldownsec = (cooldownmin * 60);
                if (debug){Puts($"cooldown in seconds, calculated from config : {cooldownsec}");}
                if (storedData.playercounter.ContainsKey(player.userID) == true)
                {
                    if (debug){Puts($"player cooldown counter increment");}
                    float counting = new float();
                    storedData.playercounter.TryGetValue(player.userID, out counting);
                    storedData.playercounter.Remove(player.userID);
                    counting = counting + trigger;
                    storedData.playercounter.Add(player.userID, counting);
                    if (debug){Puts($"player {player.userID} newtime {counting}");}
                    if (counting >= cooldownsec)
                    {
                        if (debug){Puts($"player reached cooldown. removing from dict.");}
                        storedData.playercounter.Remove(player.userID);
                    }
                    else
                    {
                        if (debug){Puts($"player new cooldown counter in minutes : {counting/60} / {cooldownmin}");}
                        storedData.playercounter.Remove(player.userID);
                        storedData.playercounter.Add(player.userID, counting);
                    }
                }
            }
        }
/////////// PLAYER SPAWNS HIS MINI RF COPTER //////////////
//////////////////
// CHAT SPAWN
//////////////////
        [ChatCommand("mymini")]         
        private void SpawnMyMinicopterChatCommand(BasePlayer player, string command, string[] args)
        {
            bool isspawner = permission.UserHasPermission(player.UserIDString, MinicopterSpawn);
            if (isspawner == false)
            {
                Player.Message(player, lang.GetMessage("NoPermMsg", this, player.UserIDString), Prefix, SteamIDIcon);
                return;
            }
            if (storedData.playerminiID.ContainsKey(player.userID) == true)
            {
                Player.Message(player, lang.GetMessage("AlreadyMsg", this, player.UserIDString), Prefix, SteamIDIcon);
                return;
            }
            bool hascooldown = permission.UserHasPermission(player.UserIDString, MinicopterCooldown);
            float minleft = 0;
            if (hascooldown == true)
            {
                if (storedData.playercounter.ContainsKey(player.userID) == false)
                {
                    storedData.playercounter.Add(player.userID, 0);
                }
                else
                {
                    float count = new float();
                    storedData.playercounter.TryGetValue(player.userID, out count);
                    minleft = cooldownmin - (count/60);
                    if (debug) Puts($"Player DID NOT reach cooldown return.");
                    Player.Message(player, $"{lang.GetMessage("CooldownMsg", this, player.UserIDString)} ({minleft} min)", Prefix, SteamIDIcon);     
                    return;
                }
            }
            else
            {
                if (storedData.playercounter.ContainsKey(player.userID))
                {
                    storedData.playercounter.Remove(player.userID);
                }
            }
            SpawnMyMinicopter(player);
        }
///////////////
// CONSOLE SPAWN
//////////////////
        [ConsoleCommand("spawnminicopter")]
        private void SpawnMyMinicopterConsoleCommand(ConsoleSystem.Arg arg)       
        {
            if (arg.Args.Length == 1)
            {
                ulong cherche = Convert.ToUInt64(arg.Args[0]);
                if (cherche == null) return;
                if (cherche.IsSteamId() == false) return;
                BasePlayer player = BasePlayer.FindByID(cherche);
                SpawnMyMinicopter(player);
            }

        }
///////////////////
// SPAWN HOOK
//////////////
        private void SpawnMyMinicopter(BasePlayer player)
        {
            Vector3 position = player.transform.position + (player.transform.forward * 5);
            if (position == null) return;
            BaseVehicle vehicleMini = (BaseVehicle)GameManager.server.CreateEntity(prefab, position, new Quaternion());
            if (vehicleMini == null) return;
            BaseEntity Minientity = vehicleMini as BaseEntity;
            Minientity.OwnerID = player.userID;
            vehicleMini.Spawn();
            Player.Message(player, $"{lang.GetMessage("SpawnedMsg", this, player.UserIDString)}", Prefix, SteamIDIcon);
            uint minicopteruint = vehicleMini.net.ID;
            if (debug) Puts($"SPAWNED MINICOPTER {minicopteruint.ToString()} for player {player.displayName} OWNER {Minientity.OwnerID}");  
            storedData.playerminiID.Remove(player.userID);
            storedData.playerminiID.Add(player.userID,minicopteruint);
            baseplayerminicop.Remove(player);
            baseplayerminicop.Add(player, vehicleMini);
        }
//////////////////
// CHAT DESPAWN
////////////////
        [ChatCommand("nomini")]         
        private void KillMyMinicopterChatCommand(BasePlayer player, string command, string[] args)
        {
            bool isspawner = permission.UserHasPermission(player.UserIDString, MinicopterSpawn);
            if (isspawner == false)
            {
                Player.Message(player, lang.GetMessage("NoPermMsg", this, player.UserIDString), Prefix, SteamIDIcon);
                return;
            }
            KillMyMinicopterPlease(player);
        }
//////////////////
// CONSOLE DESPAWN
//////////////
        [ConsoleCommand("killminicopter")]
        private void KillMyMinicopterConsoleCommand(ConsoleSystem.Arg arg)       
        {
            if (arg.Args.Length == 1)
            {
                ulong cherche = Convert.ToUInt64(arg.Args[0]);
                if (cherche == null) return;
                if (cherche.IsSteamId() == false) return;
                BasePlayer player = BasePlayer.FindByID(cherche);
                KillMyMinicopterPlease(player);
            }

        }
//////////////////////////
// KILL MINICOPTER HOOK
///////////////      
        private void KillMyMinicopterPlease(BasePlayer player)
        {
            if (storedData.playerminiID.ContainsKey(player.userID) == true)
            {
                uint deluint;
                storedData.playerminiID.TryGetValue(player.userID, out deluint);
                var tokill = BaseNetworkable.serverEntities.Find(deluint);
                if (tokill != null)
                {
                    tokill.Kill();
                }
                storedData.playerminiID.Remove(player.userID);
                baseplayerminicop.Remove(player);
            }
        }
/////////// CHAT MESSAGE TO ONLINE PLAYER with ulong //////////////

        private void ChatPlayerOnline(ulong ailldi, string message)
        {
            BasePlayer player = BasePlayer.FindByID(ailldi);
            if (player != null)
            {
                if (message == "killed") Player.Message(player, lang.GetMessage("KilledMsg", this, player.UserIDString), Prefix, SteamIDIcon);
            }
        }

////////////////////// ON KILL - chat owner /////////////////////

        void OnEntityKill(BaseNetworkable entity)
        {
            if (entity == null) return;
            if (entity.net.ID == null)return;
            MiniCopter check = entity as MiniCopter;
            if (check == null) return;
            if (storedData.playerminiID == null) return;
            ulong todelete = new ulong();
            if (storedData.playerminiID.ContainsValue(entity.net.ID) == false)
            {
                if (debug) Puts($"KILLED MINICOPTER not from myMiniCopter plugin");
                return;
            }
            foreach (var item in storedData.playerminiID)
            {
                if (item.Value == entity.net.ID)
                {
                    ChatPlayerOnline(item.Key, "killed");
                    BasePlayer player = BasePlayer.FindByID(item.Key);
                    if (player != null) baseplayerminicop.Remove(player);
                    todelete = item.Key;
                }
            }
            if (todelete != null)
            {
                storedData.playerminiID.Remove(todelete);
            }
        }
    }
}