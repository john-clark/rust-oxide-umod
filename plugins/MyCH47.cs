using UnityEngine;
using System.Collections.Generic;
using Oxide.Core;
using Convert = System.Convert;
using System.Linq;
using Oxide.Game.Rust.Cui;

namespace Oxide.Plugins
{
    [Info("My CH47", "BuzZ[PHOQUE]", "0.0.3")]
    [Description("Spawn a CH47 Helicopter")]

/*======================================================================================================================= 
*   THANKS TO THE OXIDE/UMOD TEAM for coding quality, ideas, and time spent for the community
*
*   23th november 2018
*   chat commands : /ch47    /noch47
*
*   0.0.1   20181123    creation
*   0.0.3   20181123    lang correct, /noch47 needed on kill FIX, added cooldown with perm/config, added withoutdebris config
*
*   CH47HelicopterAIController -> CH47Helicopter -> BaseHelicopterVehicle -> BaseVehicle -> BaseMountable -> BaseCombatEntity
*
*   useful notes :
*       CH47HelicopterAIController
*           AttemptMount(BasePlayer) : Void
*           CanDropCrate() : Boolean
*           DismountAllPlayers() : Void
*           DropCrate() : Void
*           GetPosition() : Vector3
*           MaintainAIAltutide() : Void // error in original mispelling, will produce error if rectified by rust devs
*           OnAttacked(HitInfo) : Void
*           OnKilled(HitInfo) : Void
*           OutOfCrates() : Boolean
*           SetDropDoorOpen(Boolean) : Void
*           public const .Flags Flag_Damaged = 32768;
*           public const .Flags Flag_DropDoorOpen = 65536;
*           public const .Flags Flag_NearDeath = 4;
*           public int32 numCrates;
*
*       BaseVehicle
*           Flag_Headlights
*
*       BaseHelicopterVehicle
*           CollisionDamageEnabled() : Boolean
*           LightToggle(BasePlayer) : Void
*           MaxVelocity() : Single
*           OnKilled(HitInfo) : Void
*           PilotInput(InputState,BasePlayer) : Void    &   PlayerServerInput(InputState,BasePlayer) : Void
*           public float32 altForceDotMin;
*           public float32 currentThrottle;
*           public float32 engineThrustMax;
*           public const .Flags Flag_InternalLights = 16384;
*           
*       BaseMountable
*           IsMounted() : Boolean
*
*           public float32 _health;
*           public float32 _maxHealth;
*
*=======================================================================================================================*/

    public class MyCH47 : RustPlugin
    {
        bool debug = false;

        string Prefix = "[My CH47] ";
        string PrefixColor = "#149800";
        string ChatColor = "#bbffb1"; 
        ulong SteamIDIcon = 76561198332562475;
        const string prefab = "assets/prefabs/npc/ch47/ch47.entity.prefab";
        private bool ConfigChanged;
        const string CH47spawn = "mych47.spawn"; 
        const string CH47cooldown = "mych47.cooldown"; 
        bool normalch47kill;
        bool withoutdebris;
        float cooldownmin = 60;
        float trigger = 60;
		private Timer clock;

        public Dictionary<BasePlayer, BaseVehicle > baseplayerch47 = new Dictionary<BasePlayer, BaseVehicle>(); //for FUTURE

    class StoredData
    {
        public Dictionary<ulong, uint> playerch47 = new Dictionary<ulong, uint>();
        public Dictionary<ulong, float> playercounter = new Dictionary<ulong, float>();
        public StoredData()
        {
        }
    }
        private StoredData storedData;

        void Init()
        {
            LoadVariables();
            permission.RegisterPermission(CH47spawn, this);
            permission.RegisterPermission(CH47cooldown, this);
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
                {"AlreadyMsg", "You already have a CH47 helicopter.\nuse command '/noch47' to remove it."},
                {"SpawnedMsg", "Your CH47 has spawned !\nuse command '/noch47' to remove it."},
                {"KilledMsg", "Your CH47 has been removed/killed."},
                {"NoPermMsg", "You are not allowed to do this."},
                {"CooldownMsg", "You must wait before a new CH47"},

            }, this, "en");

            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"AlreadyMsg", "Vous avez déjà un hélicoptère CH47\nutilisez la commande '/noch47' pour le supprimer."},
                {"SpawnedMsg", "Votre hélicoptère est arrivé !\nutilisez la commande '/noch47' pour le supprimer."},
                {"KilledMsg", "Votre CH47 a disparu du monde."},
                {"NoPermMsg", "Vous n'êtes pas autorisé."},
                {"CooldownMsg", "Vous devez attendre avant de redemander un CH47"},

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
            Prefix = Convert.ToString(GetConfig("Chat Settings", "Prefix", "[My CH47] "));                       // CHAT PLUGIN PREFIX
            PrefixColor = Convert.ToString(GetConfig("Chat Settings", "PrefixColor", "#149800"));                // CHAT PLUGIN PREFIX COLOR
            ChatColor = Convert.ToString(GetConfig("Chat Settings", "ChatColor", "#bbffb1"));                    // CHAT MESSAGE COLOR
            SteamIDIcon = Convert.ToUInt64(GetConfig("Chat Settings", "SteamIDIcon", "76561198332562475"));        // SteamID FOR PLUGIN ICON - STEAM PROFILE CREATED FOR THIS PLUGIN / NONE YET /
            withoutdebris = Convert.ToBoolean(GetConfig("Debris Settings", "Remove debris", "false"));
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
/////////// PLAYER SPAWNS HIS CH47 //////////////

        [ChatCommand("mych47")]         
        private void SpawnMyCH47(BasePlayer player, string command, string[] args)
        {
            bool isspawner = permission.UserHasPermission(player.UserIDString, CH47spawn);
            if (isspawner == false)
            {
                Player.Message(player, $"<color={ChatColor}><i>{lang.GetMessage("NoPermMsg", this, player.UserIDString)}</i></color>",$"<color={PrefixColor}> {Prefix} </color>", SteamIDIcon);
                return;
            }
            if (storedData.playerch47.ContainsKey(player.userID) == true)
            {
                Player.Message(player, $"<color={ChatColor}>{lang.GetMessage("AlreadyMsg", this, player.UserIDString)}</color>",$"<color={PrefixColor}> {Prefix} </color>", SteamIDIcon);
                return;
            }
            bool hascooldown = permission.UserHasPermission(player.UserIDString, CH47cooldown);
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
                        if (debug == true) {Puts($"Player DID NOT reach cooldown return.");}
                        Player.Message(player, $"<color={ChatColor}>{lang.GetMessage("CooldownMsg", this, player.UserIDString)} ({minleft} min)</color>",$"<color={PrefixColor}> {Prefix} </color>", SteamIDIcon);     
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
            Vector3 position = player.transform.position + (player.transform.forward * 20);
            if (position == null) return;
            BaseVehicle vehicleCH47 = (BaseVehicle)GameManager.server.CreateEntity(prefab, position, new Quaternion());
            if (vehicleCH47 == null) return;

            BaseEntity CHentity = vehicleCH47 as BaseEntity;
            CHentity.OwnerID = player.userID;
////// SET PROPERTIES ON SPAWN - FOR FUTURE

            vehicleCH47.Spawn();

            Player.Message(player, $"<color={ChatColor}>{lang.GetMessage("SpawnedMsg", this, player.UserIDString)}</color>",$"<color={PrefixColor}> {Prefix} </color>", SteamIDIcon);

//////////////////
// BONUS ENTITIES SPAWN - FOR FUTURE
//////////////////

            uint ch47uint = vehicleCH47.net.ID;
            if (debug == true) {Puts($"SPAWNED CH47 {ch47uint.ToString()} for player {player.displayName} OWNER {CHentity.OwnerID}");}

            storedData.playerch47.Remove(player.userID);
            storedData.playerch47.Add(player.userID,ch47uint);
            baseplayerch47.Remove(player);
            baseplayerch47.Add(player, vehicleCH47);
        }

        [ChatCommand("noch47")]         
        private void KillMyCH47(BasePlayer player, string command, string[] args)
        {
            bool isspawner = permission.UserHasPermission(player.UserIDString, CH47spawn);
            if (isspawner == false)
            {
                Player.Message(player, $"<color={ChatColor}><i>{lang.GetMessage("NoPermMsg", this, player.UserIDString)}</i></color>",$"<color={PrefixColor}> {Prefix} </color>", SteamIDIcon);
                return;
            }
            if (storedData.playerch47.ContainsKey(player.userID) == true)
            {
                uint deluint;
                storedData.playerch47.TryGetValue(player.userID, out deluint);
                var tokill = BaseNetworkable.serverEntities.Find(deluint);
                if (tokill != null)
                {
                    tokill.Kill();
                }
                storedData.playerch47.Remove(player.userID);
                baseplayerch47.Remove(player);
            }
        }

/////////// CHAT MESSAGE TO ONLINE PLAYER with ulong //////////////

        private void ChatPlayerOnline(ulong ailldi, string message)
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList.ToList())
            {
                if (player.userID == ailldi)
                {
                    if (message == "killed")
                    {
                        Player.Message(player, $"<color={ChatColor}>{lang.GetMessage("KilledMsg", this, player.UserIDString)}</color>",$"<color={PrefixColor}> {Prefix} </color>", SteamIDIcon);
                    }
                }
            }
        }

        void OnEntitySpawned(BaseNetworkable entity)
        {
            if (!withoutdebris) return;
            if (normalch47kill == true)
            {
                if (debug == true) {Puts($"IGNORING DEBRIS REMOVAL - NORMAL CH47 KILLED");}
                return;
            }
            if (entity == null) return;
            var prefabname = entity.ShortPrefabName;
            if (string.IsNullOrEmpty(prefabname)) return;
            if (entity is HelicopterDebris && prefabname.Contains("ch47"))
            {
                var debris = entity.GetComponent<HelicopterDebris>();
                if (debris == null) return;
                entity.Kill();
                if (debug == true) {Puts($"REMOVED DEBRIS FROM myCH47 KILLED");}
            }
        }
////////////////////// ON KILL - chat owner /////////////////////

        void OnEntityKill(BaseNetworkable entity)
        {
            if (entity == null) return;
            if (entity.net.ID == null)return;
            CH47Helicopter check = entity as CH47Helicopter;
            if (check == null) return;
            if (storedData.playerch47 == null) return;
            ulong todelete = new ulong();
            if (storedData.playerch47.ContainsValue(entity.net.ID) == false)
            {
                if (debug == true) {Puts($"KILLED CH47 not from myCH47");}
                normalch47kill = true;
                timer.Once(6f, () =>
                {
                    normalch47kill = false;
                });
            }
            foreach (var item in storedData.playerch47)
            {
                if (item.Value == entity.net.ID)
                {
                    ChatPlayerOnline(item.Key, "killed");
                    foreach (BasePlayer player in BasePlayer.activePlayerList.ToList())
                    {
                        if (player.userID == item.Key)
                        {
                            baseplayerch47.Remove(player);
                        }                       
                    }
                    todelete = item.Key;
                }
            }
            if (todelete != null)
            {
                storedData.playerch47.Remove(todelete);
            }
        }
    }
}