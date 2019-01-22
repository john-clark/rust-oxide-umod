// Requires: HumanNPC
// Requires: PathFinding

using System.Collections.Generic;
using System;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Core.Configuration;
using UnityEngine;
using Newtonsoft.Json.Linq;
using System.Linq;
using Oxide.Core.Libraries.Covalence;
using static UnityEngine.Vector3;
using Oxide.Game.Rust.Cui;
using System.Globalization;
using System.Reflection;


namespace Oxide.Plugins
{
    [Info("Npctp", "Ts3hosting", "2.4.6")]
    [Description("Some NPC Controle Thanks Wulf and k1lly0u")]
    class Npctp : RustPlugin
    {
        #region Initialization

        [PluginReference]
        Plugin Spawns, Economics, ServerRewards, Vanish, HumanNPC, Jail;

        PlayerCooldown pcdData;
        NPCTPDATA npcData;
        private DynamicConfigFile PCDDATA;
        private DynamicConfigFile NPCDATA;
        private FieldInfo serverinput;
        private bool backroundimage;
        private string backroundimageurl;
        private static int cooldownTime = 3600;
        private static int auth = 2;
        private bool Changed;
        private float Cost = 0;
        private int ItemAmount = 0;		
        private float DoorLocX = 0;
        private float DoorLocY = 0;
        private float DoorLocZ = 0;
        private uint DoorId = 0;
        private static bool useEconomics = false;
        private static bool useRewards = false;
        private static bool useItem = false;		
        private static bool useJail = true;
        private static bool AutoCloseDoors = true;
        private static int AutoCloseTime = 10;
        private static int SleepKillDamage = 10;
        private static bool DoorOpenMessage = true;
        private static bool FollowDead = true;
        private static bool UseGunToKill = true;
        private static bool SetOnFire = false;
        private static int OnFireTime = 10;
        private static int OnFireDamage = 10;
        private string NpcName = "<color=orange>Npc</color> : ";
        private string IDNPC = "";
        private string msg = "";
        private string SpawnFiles = "";


        #region Localization       
        Dictionary<string, string> messages = new Dictionary<string, string>()
        {
            {"title", "<color=orange>Npc</color> : "},
            {"cdTime", "You must wait another {0} minutes and some seconds before using me again" },
            {"noperm", "You do not have permissions to talk to me!" },
            {"notenabled", "Sorry i am not enabled!" },
            {"nomoney", "Sorry you need {0} to talk to me!" },
            {"charged", "Thanks i only took {0} from you!" },
            {"npcCommand", "I just ran a Command!" },
            {"npcadd", "Added npcID {0} to datafile and not enabled edit NpcTP_Data ." },
            {"npcadds", "Added npcID {0} with spawnfile {1} to datafile and enabled edit NpcTP_Data for more options." },
            {"npcerror", "error example /npctp_add <npcID> <spawnfile> or /npctcp_add <npcID>" },
            {"nopermcmd", "You do not have permissions to use this command!" },
            {"commandyesno", "This will cost you " },
            {"commandyesno1", " do you want to pay?" },
            {"notfound", "The npc was not found check npcID" },
            {"npchelp", "Error: use /npctp <npcID>" },
            {"DeadCmd", "Sorry you can not kill me again that fast. Wait {0} seconds." },
            {"doorerror", "error example /npctp_door <npcID>" },
            {"DoorIsOpen", "The Door is already open." },
            {"DoorNotFound", "The Door Was Not Found!" },
            {"DoorMessage", "I Just opened the door for you." },
            {"chargedItem", "Thanks i only took {0} {1} from you!" }			

       };
        #endregion


        void Loaded()
        {
            LoadData();
            LoadVariables();
            RegisterPermissions();
            CheckDependencies();
        }
		void Init()
		{
            serverinput = typeof(BasePlayer).GetField("serverInput", (BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic));
            PCDDATA = Interface.Oxide.DataFileSystem.GetFile("NpcTp/NpcTP_Player");
            NPCDATA = Interface.Oxide.DataFileSystem.GetFile("NpcTp/NpcTP_Data");
            lang.RegisterMessages(messages, this);
            Puts("Thanks for using NPCTP drop me a line if you need anything added.");		
	}

        void Unload()
        {
            foreach (BasePlayer current in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(current, "Npctp");
            }
        }

        object GetConfig(string menu, string datavalue, object defaultValue)
        {
            var data = Config[menu] as Dictionary<string, object>;
            if (data == null)
            {
                data = new Dictionary<string, object>();
                Config[menu] = data;
                Changed = true;
            }
            object value;
            if (!data.TryGetValue(datavalue, out value))
            {
                value = defaultValue;
                data[datavalue] = value;
                Changed = true;
            }
            return value;
        }

        private void RegisterPermissions()
        {
            permission.RegisterPermission("npctp.admin", this);
            permission.RegisterPermission("npctp.default", this);
            foreach (var perm in npcData.NpcTP.Values)
            {
                if (!string.IsNullOrEmpty(perm.permission) && !permission.PermissionExists(perm.permission))
                    permission.RegisterPermission(perm.permission, this);
            }
        }

        private void CheckDependencies()
        {
            if (Economics == null)
                if (useEconomics)
                {
                    PrintWarning($"Economics could not be found! Disabling money feature");
                    useEconomics = false;
                }
            if (ServerRewards == null)
                if (useRewards)
                {
                    PrintWarning($"ServerRewards could not be found! Disabling RP feature");
                    useRewards = false;
                }
            if (Jail == null)
                if (useJail)
                {
                    PrintWarning($"Jail could not be found! Disabling Jail Protection");
                    useJail = false;
                }                
            if (Spawns == null)
            {
                PrintWarning($"Spawns Database could not be found you only can use command NPC or random spawns!");
            }
        }

        void LoadVariables()
        {
            useEconomics = Convert.ToBoolean(GetConfig("SETTINGS", "useEconomics", false));
            useRewards = Convert.ToBoolean(GetConfig("SETTINGS", "useRewards", false));
			useItem = Convert.ToBoolean(GetConfig("SETTINGS", "useItem", false));
            AutoCloseDoors = Convert.ToBoolean(GetConfig("SETTINGS", "AutoCloseDoors", true));
            DoorOpenMessage = Convert.ToBoolean(GetConfig("SETTINGS", "DoorOpenMessage", true));
            AutoCloseTime = Convert.ToInt32(GetConfig("SETTINGS", "AutoCloseTime", 15));
            SleepKillDamage = Convert.ToInt32(GetConfig("KillSleepPlayer", "SleepKillDamage", 10));
            UseGunToKill = Convert.ToBoolean(GetConfig("KillNoPerms", "UseGunToKill", true));
            FollowDead = Convert.ToBoolean(GetConfig("KillNoPerms", "NpcFollowUntellDead", false));
            SetOnFire = Convert.ToBoolean(GetConfig("KillNoPerms", "SetOnFire", false));
            OnFireTime = Convert.ToInt32(GetConfig("KillNoPerms", "OnFireTime", 10));
            OnFireDamage = Convert.ToInt32(GetConfig("KillNoPerms", "OnFireDamage", 10));

            if (Changed)
            {
                SaveConfig();
                Changed = false;
            }
        }

        protected override void LoadDefaultConfig()
        {
            Puts("Creating a new configuration file!");
            Config.Clear();
            LoadVariables();
        }
        #endregion

        #region Classes and Data Management    
        void SaveNpcTpData()
        {
            NPCDATA.WriteObject(npcData);
        }

        class NPCTPDATA
        {
            public Dictionary<string, NPCInfo> NpcTP = new Dictionary<string, NPCInfo>();

            public NPCTPDATA() { }
        }
        class NPCInfo
        {
            public string NpcName;
            public string SpawnFile;
            public int Cooldown;
            public bool CanUse;
            public bool useUI;
            public float Cost;
            public string permission;
            public bool useItem;
			public string ItemShortName;			
			public int ItemAmount;
            public bool UseCommand;			
            public bool CommandOnPlayer;
            public string Command;
            public string Arrangements;
            public bool useMessage;
            public string MessageNpc;
            public bool EnableDead;
            public bool DeadOnPlayer;
            public string DeadCmd;
            public string DeadArgs;
            public bool OpenDoor;
            public float DoorLocX;
            public float DoorLocY;
            public float DoorLocZ;
            public uint DoorId;
            public bool NoPermKill;
            public bool KillSleep;
        }

        class PlayerCooldown
        {
            public Dictionary<ulong, PCDInfo> pCooldown = new Dictionary<ulong, PCDInfo>();


            public PlayerCooldown() { }
        }
        class PCDInfo
        {

            public Dictionary<string, long> npcCooldowns = new Dictionary<string, long>();

            public PCDInfo() { }
            public PCDInfo(long cd)
            {
            }
        }

        void SaveData()
        {
            PCDDATA.WriteObject(pcdData);
        }
        void LoadData()
        {
            try
            {
                pcdData = Interface.Oxide.DataFileSystem.ReadObject<PlayerCooldown>("NpcTp/NpcTP_Player");
            }
            catch
            {
                Puts("Couldn't load NPCTP data, creating new Playerfile");
                pcdData = new PlayerCooldown();
            }
            try
            {
                npcData = Interface.GetMod().DataFileSystem.ReadObject<NPCTPDATA>("NpcTp/NpcTP_Data");
            }
            catch
            {
                Puts("Couldn't load NPCTP data, creating new datafile");
                npcData = new NPCTPDATA();
            }
        }

        #endregion


        #region Cooldown Management       

        static double GrabCurrentTime() => DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1, 0, 0, 0)).TotalSeconds;

        #endregion
        #region npctp_door
        [ChatCommand("npctp_door")]
        void SetDoor(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.userID.ToString(), "npctp.admin"))
            {
                SendReply(player, string.Format(lang.GetMessage(NpcName, this) + lang.GetMessage("nopermcmd", this)));
                return;
            }
            if (args.Length <= 0)
            {
                SendReply(player, string.Format(lang.GetMessage(NpcName, this) + lang.GetMessage("doorerror", this, player.UserIDString)));
                return;
            }
            var n = npcData.NpcTP;
            var npcId = (args[0]);
            var input = serverinput.GetValue(player) as InputState;
            var currentRot = Quaternion.Euler(input.current.aimAngles) * Vector3.forward;
            Vector3 eyesAdjust = new Vector3(0f, 1.5f, 0f);
            var rayResult = CastRay(player.transform.position + eyesAdjust, currentRot);
            if (!n.ContainsKey(npcId))
            {
                SendReply(player, "There is no NpcID {0} is the id correct and /npctp_add in the datafile", npcId);
                return;
            }
            if (rayResult is BaseEntity)
            {
                var entity = rayResult as BaseEntity;
                var DoorId = entity.net.ID;
                var DoorLocX = entity.transform.position.x;
                var DoorLocY = entity.transform.position.y;
                var DoorLocZ = entity.transform.position.z;
                if (entity.GetComponent<Door>())
                    npcData.NpcTP[npcId].DoorId = DoorId;
                npcData.NpcTP[npcId].DoorLocX = DoorLocX;
                npcData.NpcTP[npcId].DoorLocY = DoorLocY;
                npcData.NpcTP[npcId].DoorLocZ = DoorLocZ;
                npcData.NpcTP[npcId].OpenDoor = true;
                SaveNpcTpData();
                SendReply(player, "Door added to NPCID {0}", npcId);
                return;
            }
            SendReply(player, "This is not a door or invalid NpcID {0}", npcId);
        }
        #endregion
        #region ItemAmount
        private int GetAmount(BasePlayer player, string shortname)
        {
            List<Item> items = player.inventory.AllItems().ToList().FindAll(x => x.info.shortname == shortname);
            int num = 0;
            foreach (Item item in items)
            {
                if (!item.IsBusy())
                {
                    if (!item.IsBusy())
                        num = num + item.amount;
                }
            }
			
            return num;
        }
		
        private bool TakeResources(BasePlayer player, string shortname, int iAmount)
        {
            int num = TakeResourcesFrom(player, player.inventory.containerMain.itemList, shortname, iAmount);
            if (num < iAmount)
                num += TakeResourcesFrom(player, player.inventory.containerBelt.itemList, shortname, iAmount);
            if (num < iAmount)
                num += TakeResourcesFrom(player, player.inventory.containerWear.itemList, shortname, iAmount);
            if (num >= iAmount)
                return true;
            return false;
        }
        private int TakeResourcesFrom(BasePlayer player, List<Item> container, string shortname, int iAmount)
        {
            List<Item> collect = new List<Item>();
            List<Item> items = new List<Item>();
            int num = 0;
            foreach (Item item in container)
            {
                if (item.info.shortname == shortname)
                {
                    int num1 = iAmount - num;
                    if (num1 > 0)
                    {
                        if (item.amount <= num1)
                        {
                            if (item.amount <= num1)
                            {
                                num = num + item.amount;
                                items.Add(item);
                                if (collect != null)
                                    collect.Add(item);
                            }
                            if (num != iAmount)
                                continue;
                            break;
                        }
                        else
                        {
                            item.MarkDirty();
                            Item item1 = item;
                            item1.amount = item1.amount - num1;
                            num = num + num1;
                            Item item2 = ItemManager.CreateByName(shortname, 1);
                            item2.amount = num1;
                            item2.CollectedForCrafting(player);
                            if (collect != null)
                                collect.Add(item2);
                            break;
                        }
                    }
                }
            }
            foreach (Item item3 in items)
                item3.RemoveFromContainer();
            return num;
        }		
		
		
        #endregion
        #region Teleport

        object CastRay(Vector3 Pos, Vector3 Aim)
        {
            var hits = Physics.RaycastAll(Pos, Aim);
            float distance = 100;
            object target = null;

            foreach (var hit in hits)
            {
                if (hit.collider.GetComponentInParent<BaseEntity>() != null)
                {
                    if (hit.distance < distance)
                    {
                        distance = hit.distance;
                        target = hit.collider.GetComponentInParent<BaseEntity>();
                    }
                }
            }
            return target;
        }
        private object ProcessRay(RaycastHit hitInfo)
        {
            if (hitInfo.collider != null)
            {
                if (hitInfo.collider?.gameObject.layer == UnityEngine.LayerMask.NameToLayer("Water"))
                    return null;
                if (hitInfo.collider?.gameObject.layer == UnityEngine.LayerMask.NameToLayer("Prevent Building"))
                    return null;
                if (hitInfo.GetEntity() != null)
                {
                    return hitInfo.point.y;
                }
                if (hitInfo.collider?.name == "areaTrigger")
                    return null;
                if (hitInfo.collider?.GetComponentInParent<SphereCollider>() || hitInfo.collider?.GetComponentInParent<BoxCollider>())
                {
                    return hitInfo.collider.transform.position + new Vector3(0, -1, 0);
                }
            }
            return hitInfo.point.y;
        }
        RaycastHit RayPosition(Vector3 sourcePos)
        {
            RaycastHit hitInfo;
            Physics.Raycast(sourcePos, Vector3.down, out hitInfo);

            return hitInfo;
        }

        private void TeleportPlayerPosition1(BasePlayer player, Vector3 destination)
        {
            if (player.net?.connection != null)
                player.ClientRPCPlayer(null, player, "StartLoading");
            player.inventory.crafting.CancelAll(true);
            StartSleeping(player);
            player.MovePosition(destination);
            if (player.net?.connection != null)
                player.ClientRPCPlayer(null, player, "ForcePositionTo", destination);
            if (player.net?.connection != null)
                player.SetPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot, true);
            player.UpdateNetworkGroup();
            player.SendNetworkUpdateImmediate(false);
            if (player.net?.connection == null) return;
            try { player.ClearEntityQueue(null); } catch { }
            player.SendFullSnapshot();
        }
        private void StartSleeping(BasePlayer player)
        {
            if (player.IsSleeping())
                return;
            player.SetPlayerFlag(BasePlayer.PlayerFlags.Sleeping, true);
            if (!BasePlayer.sleepingPlayerList.Contains(player))
                BasePlayer.sleepingPlayerList.Add(player);
            player.CancelInvoke("InventoryUpdate");
        }

        object GetRandomVector(Vector3 position, float max, bool failed = false)
        {
            var targetPos = UnityEngine.Random.insideUnitCircle * max;
            var sourcePos = new Vector3(position.x + targetPos.x, 300, position.z + targetPos.y);
            var hitInfo = RayPosition(sourcePos);
            var success = ProcessRay(hitInfo);
            if (success == null)
            {
                return GetRandomVector(position, max, true);
            }
            else if (success is Vector3)
            {
                return GetRandomVector(new Vector3(sourcePos.x, ((Vector3)success).y, sourcePos.y), max, true);
            }
            else
            {
                sourcePos.y = Mathf.Max((float)success, TerrainMeta.HeightMap.GetHeight(sourcePos));
                return sourcePos;
            }
        }
        #endregion


		
        #region USENPC

        [ChatCommand("npctp_add")]
        void cmdNpcAdD(BasePlayer player, string command, string[] args)
        {
            var n = npcData.NpcTP;
            if (!permission.UserHasPermission(player.userID.ToString(), "npctp.admin"))
            {
                SendReply(player, string.Format(lang.GetMessage(NpcName, this) + lang.GetMessage("nopermcmd", this)));
                return;
            }
            if (args.Length == 2)
            {
                SpawnFiles = (args[1]);
            }

            var setup = new NPCInfo { Cost = Cost, CanUse = false, useUI = false, permission = "npctp.default", UseCommand = false, useItem = false, ItemShortName = "ammo.rifle.hv", ItemAmount = ItemAmount, CommandOnPlayer = false, Command = "say", Arrangements = "this is a test", useMessage = false, MessageNpc = "none", EnableDead = false, DeadOnPlayer = false, DeadCmd = "jail.send", DeadArgs = "5", OpenDoor = false, DoorLocX = DoorLocX, DoorLocY = DoorLocY, DoorLocZ = DoorLocZ, DoorId = DoorId, NpcName = NpcName, NoPermKill = false, KillSleep = false };
            var setups = new NPCInfo { Cost = Cost, CanUse = true, useUI = false, SpawnFile = SpawnFiles, permission = "npctp.default", UseCommand = false, useItem = false, ItemShortName = "ammo.rifle.hv", ItemAmount = ItemAmount, CommandOnPlayer = false, Command = "say", Arrangements = "this is a test", useMessage = false, MessageNpc = "none", EnableDead = false, DeadOnPlayer = false, DeadCmd = "jail.send", DeadArgs = "5", OpenDoor = false, DoorLocX = DoorLocX, DoorLocY = DoorLocY, DoorLocZ = DoorLocZ, DoorId = DoorId, NpcName = NpcName, NoPermKill = false, KillSleep = false };

            if (args.Length <= 0)
            {
                SendReply(player, string.Format(lang.GetMessage(NpcName, this) + lang.GetMessage("npcerror", this, player.UserIDString)));
                return;
            }

            IDNPC = (args[0]);

            if (args.Length == 1)
            {
                if (!n.ContainsKey(IDNPC))
                    n.Add(IDNPC, setup);
                SaveNpcTpData();
                SendReply(player, string.Format(lang.GetMessage(NpcName, this) + lang.GetMessage("npcadd", this, player.UserIDString), (string)(IDNPC)));
                return;
            }
            if (args.Length == 2)
            {
                if (!n.ContainsKey(IDNPC))
                    n.Add(IDNPC, setups);
                SaveNpcTpData();
                SendReply(player, string.Format(lang.GetMessage(NpcName, this) + lang.GetMessage("npcadds", this, player.UserIDString), (string)(IDNPC), (string)(SpawnFiles)));
                return;
            }
        }

        [ChatCommand("npctp")]
        void cmdChatNPCEdit(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.userID.ToString(), "npctp.admin"))
            {
                SendReply(player, string.Format(lang.GetMessage(NpcName, this) + lang.GetMessage("nopermcmd", this)));
                return;
            }
            if (args.Length <= 0)
            {
                SendReply(player, string.Format(lang.GetMessage(NpcName, this) + lang.GetMessage("npchelp", this, player.UserIDString)));
                return;
            }
            var n = npcData.NpcTP;
            string npcId = (args[0]);
            int newvalueint = 0;
            bool newbool = false;
            float newfloat = 0;
            string newvalue = "";

            if (args.Length == 1 && n.ContainsKey(npcId))
            {
                SendReply(player, "====== Settings ======");
                SendReply(player, "SpawnFile" + ": " + npcData.NpcTP[npcId].SpawnFile);
                SendReply(player, "Cooldown" + ": " + npcData.NpcTP[npcId].Cooldown);
                SendReply(player, "Cost" + ": " + npcData.NpcTP[npcId].Cost);
                SendReply(player, "CanUse" + ": " + npcData.NpcTP[npcId].CanUse);
                SendReply(player, "useUI" + ": " + npcData.NpcTP[npcId].useUI);
                SendReply(player, "permission" + ": " + npcData.NpcTP[npcId].permission);
                SendReply(player, "UseCommand" + ": " + npcData.NpcTP[npcId].UseCommand);
                SendReply(player, "CommandOnPlayer" + ": " + npcData.NpcTP[npcId].CommandOnPlayer);
                SendReply(player, "Command" + ": " + npcData.NpcTP[npcId].Command);
                SendReply(player, "Arrangements" + ": " + npcData.NpcTP[npcId].Arrangements);
                SendReply(player, "useMessage" + ": " + npcData.NpcTP[npcId].useMessage);
                SendReply(player, "MessageNpc" + ": " + npcData.NpcTP[npcId].MessageNpc);
                SendReply(player, "EnableDead" + ": " + npcData.NpcTP[npcId].EnableDead);
                SendReply(player, "DeadOnPlayer" + ": " + npcData.NpcTP[npcId].DeadOnPlayer);
                SendReply(player, "DeadCmd" + ": " + npcData.NpcTP[npcId].DeadCmd);
                SendReply(player, "DeadArgs" + ": " + npcData.NpcTP[npcId].DeadArgs);
                SendReply(player, "OpenDoor" + ": " + npcData.NpcTP[npcId].OpenDoor);
                SendReply(player, "====== End Settings ======");
                SendReply(player, "To change /npctp" + " " + npcId + " " + "<setting>" + " " + "<NewValue>");
                return;
            }

            if (!n.ContainsKey(npcId))
            {
                SendReply(player, string.Format(lang.GetMessage("notfound", this)));
                return;
            }
            string change = (args[1]).ToLower();
            if (args.Length >= 3 && change == "cooldown")
            {
                newvalue = (args[2]);
                newvalueint = Convert.ToInt32(newvalue);
                npcData.NpcTP[npcId].Cooldown = newvalueint;
                SaveNpcTpData();
                SendReply(player, "Cooldown value changed to {0}", newvalueint);
                return;
            }
            if (args.Length >= 3 && change == "spawnfile")
            {
                newvalue = (args[2]);
                npcData.NpcTP[npcId].SpawnFile = newvalue;
                SaveNpcTpData();
                SendReply(player, "SpawnFile value changed to {0}", newvalue);
                return;
            }
            if (args.Length >= 3 && change == "permission")
            {
                newvalue = (args[2]);
                npcData.NpcTP[npcId].permission = newvalue;
                SaveNpcTpData();
                SendReply(player, "permission value changed to {0}", newvalue);
                return;
            }
            if (args.Length >= 3 && change == "command")
            {
                newvalue = (args[2]);
                npcData.NpcTP[npcId].Command = newvalue;
                SaveNpcTpData();
                SendReply(player, "Coommand value changed to {0}", newvalue);
                return;
            }
            if (args.Length >= 3 && change == "arrangements")
            {
                newvalue = (args[2]);
                npcData.NpcTP[npcId].Arrangements = newvalue;
                SaveNpcTpData();
                SendReply(player, "Arrangements value changed to {0}", newvalue);
                return;
            }
            if (args.Length >= 3 && change == "messagenpc")
            {
                newvalue = (args[2]);
                npcData.NpcTP[npcId].MessageNpc = newvalue;
                SaveNpcTpData();
                SendReply(player, "MessageNpc value changed to {0}", newvalue);
                return;
            }
            if (args.Length >= 3 && change == "deadcmd")
            {
                newvalue = (args[2]);
                npcData.NpcTP[npcId].DeadCmd = newvalue;
                SaveNpcTpData();
                SendReply(player, "DeadCmd value changed to {0}", newvalue);
                return;
            }
            if (args.Length >= 3 && change == "deadargs")
            {
                newvalue = (args[2]);
                npcData.NpcTP[npcId].DeadArgs = newvalue;
                SaveNpcTpData();
                SendReply(player, "DeadArgs value changed to {0}", newvalue);
                return;
            }
            if (args.Length >= 3 && change == "canuse")
            {
                newvalue = (args[2]);
                if (newvalue == "true" || newvalue == "false")
                    newbool = Convert.ToBoolean(newvalue);
                npcData.NpcTP[npcId].CanUse = newbool;
                SaveNpcTpData();
                SendReply(player, "canUse value changed to {0}", newbool);
                return;
                {
                    SendReply(player, "{0} is not true or false try again", newbool);
                    return;
                }
            }
            if (args.Length >= 3 && change == "useui")
            {
                newvalue = (args[2]);
                if (newvalue == "true" || newvalue == "false")
                    newbool = Convert.ToBoolean(newvalue);
                npcData.NpcTP[npcId].useUI = newbool;
                SaveNpcTpData();
                SendReply(player, "useUI value changed to {0}", newbool);
                return;
                {
                    SendReply(player, "{0} is not true or false try again", newbool);
                    return;
                }
            }
            if (args.Length >= 3 && change == "usecommand")
            {
                newvalue = (args[2]);
                if (newvalue == "true" || newvalue == "false")
                    newbool = Convert.ToBoolean(newvalue);
                npcData.NpcTP[npcId].UseCommand = newbool;
                SaveNpcTpData();
                SendReply(player, "UseCommand value changed to {0}", newbool);
                return;
                {
                    SendReply(player, "{0} is not true or false try again", newbool);
                    return;
                }
            }
            if (args.Length >= 3 && change == "commandonplayer")
            {
                newvalue = (args[2]);
                if (newvalue == "true" || newvalue == "false")
                    newbool = Convert.ToBoolean(newvalue);
                npcData.NpcTP[npcId].CommandOnPlayer = newbool;
                SaveNpcTpData();
                SendReply(player, "CommandOnPlayer value changed to {0}", newbool);
                return;
                {
                    SendReply(player, "{0} is not true or false try again", newbool);
                    return;
                }
            }
            if (args.Length >= 3 && change == "usemessage")
            {
                newvalue = (args[2]);
                if (newvalue == "true" || newvalue == "false")
                    newbool = Convert.ToBoolean(newvalue);
                npcData.NpcTP[npcId].useMessage = newbool;
                SaveNpcTpData();
                SendReply(player, "useMessage value changed to {0}", newbool);
                return;
                {
                    SendReply(player, "{0} is not true or false try again", newbool);
                    return;
                }
            }
            if (args.Length >= 3 && change == "enabledead")
            {
                newvalue = (args[2]);
                if (newvalue == "true" || newvalue == "false")
                    newbool = Convert.ToBoolean(newvalue);
                npcData.NpcTP[npcId].EnableDead = newbool;
                SaveNpcTpData();
                SendReply(player, "EnableDead value changed to {0}", newbool);
                return;
                {
                    SendReply(player, "{0} is not true or false try again", newbool);
                    return;
                }
            }
            if (args.Length >= 3 && change == "deadonplayer")
            {
                newvalue = (args[2]);
                if (newvalue == "true" || newvalue == "false")
                    newbool = Convert.ToBoolean(newvalue);
                npcData.NpcTP[npcId].DeadOnPlayer = newbool;
                SaveNpcTpData();
                SendReply(player, "DeadOnPlayer value changed to {0}", newbool);
                return;
                {
                    SendReply(player, "{0} is not true or false try again", newbool);
                    return;
                }
            }
            if (args.Length >= 3 && change == "opendoor")
            {
                newvalue = (args[2]);
                if (newvalue == "true" || newvalue == "false")
                    newbool = Convert.ToBoolean(newvalue);
                npcData.NpcTP[npcId].OpenDoor = newbool;
                SaveNpcTpData();
                SendReply(player, "OpenDoor value changed to {0}", newbool);
                return;
                {
                    SendReply(player, "{0} is not true or false try again", newbool);
                    return;
                }
            }
            if (args.Length >= 3 && change == "cost")
            {
                newvalue = (args[2]);
                newfloat = float.Parse(newvalue);
                npcData.NpcTP[npcId].Cost = newfloat;
                SaveNpcTpData();
                SendReply(player, "Cost value changed to {0}", newfloat);
                return;
            }
        }

        void OnKillNPC(BasePlayer npc, HitInfo hinfo)
        {

            if (!npcData.NpcTP.ContainsKey(npc.UserIDString)) return; // Check if this NPC is registered
            var attacker = hinfo.Initiator as BasePlayer;
            if (attacker == null) return;

            var player = hinfo.Initiator.ToPlayer();
            ulong playerId = player.userID;
            string npcId = npc.UserIDString;
            var EnableDead = npcData.NpcTP[npcId].EnableDead;
            var DeadOnPlayer = npcData.NpcTP[npcId].DeadOnPlayer;
            string DeadCmd = npcData.NpcTP[npcId].DeadCmd;
            string DeadArgs = npcData.NpcTP[npcId].DeadArgs;
            string NpcName = npcData.NpcTP[npcId].NpcName;
            double timeStamp = GrabCurrentTime();
            var cooldownTime = npcData.NpcTP[npcId].Cooldown;
            if (!EnableDead) return;
            if (!pcdData.pCooldown.ContainsKey(playerId))
            {
                pcdData.pCooldown.Add(playerId, new PCDInfo());
                //SaveData();
            }
            if (pcdData.pCooldown[playerId].npcCooldowns.ContainsKey(npcId)) // Check if the player already has a cooldown for this NPC
            {
                var cdTime = pcdData.pCooldown[playerId].npcCooldowns[npcId]; // Get the cooldown time of the NPC
                if (cdTime > timeStamp)
                {
                    SendReply(player, string.Format(lang.GetMessage(NpcName, this) + lang.GetMessage("DeadCmd", this, player.UserIDString), (int)(cdTime - timeStamp)));
                    return;
                }
            }

            if (EnableDead)
                if (!DeadOnPlayer) // Check if this is command on player
                {
                    pcdData.pCooldown[playerId].npcCooldowns[npcId] = (long)timeStamp + cooldownTime; // Store the new cooldown in the players data under the specified NPC
                    SaveData();
                    rust.RunServerCommand($"{DeadCmd} {DeadArgs}");

                }

            if (DeadOnPlayer) // Check if this is command on player
            {
                pcdData.pCooldown[playerId].npcCooldowns[npcId] = (long)timeStamp + cooldownTime; // Store the new cooldown in the players data under the specified NPC
                SaveData();
                rust.RunServerCommand($"{DeadCmd} {playerId} {DeadArgs}");
            }
        }

        [ConsoleCommand("hardestcommandtoeverguessnpctp")]
        void cmdRun(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null)
			{
				return;
			}			
            var player = arg.Player();
            var npcId = arg.Args[1];
            string spawn = npcData.NpcTP[npcId].SpawnFile;
            var cooldownTime = npcData.NpcTP[npcId].Cooldown;
            var UseCommand = npcData.NpcTP[npcId].UseCommand;
            var useItem = npcData.NpcTP[npcId].useItem;
			string ItemShortName = npcData.NpcTP[npcId].ItemShortName;
			var ItemAmounts = npcData.NpcTP[npcId].ItemAmount;
            ulong playerId = player.userID;
            string Command = npcData.NpcTP[npcId].Command;
            string Arrangements = npcData.NpcTP[npcId].Arrangements;
            var CommandOnPlayer = npcData.NpcTP[npcId].CommandOnPlayer;
            var DoorId = npcData.NpcTP[npcId].DoorId;
            var doorcheck = Convert.ToString(DoorId);
            var OpenDoor = npcData.NpcTP[npcId].OpenDoor;
            var buyMoney1 = npcData.NpcTP[npcId].Cost;
            string NpcName = npcData.NpcTP[npcId].NpcName;
            var bad = "somthing is not right somewhere with the teleportation";
            var bad1 = "somthing is not right somewhere on command not on player";
            var bad2 = "somthing is not right somewhere on command on player";
            float max = TerrainMeta.Size.x / 2;
            var time = 20;
            double timeStamp = GrabCurrentTime();
            

            if (useEconomics)
            {
                double money = (double)Economics?.CallHook("Balance", player.userID);
                if (money < buyMoney1)
                {
                    SendReply(player, string.Format(lang.GetMessage(NpcName, this) + lang.GetMessage("nomoney", this, player.UserIDString), (int)(buyMoney1)));
                    return;
                }

                if (money >= buyMoney1)
                {
                    money = money - buyMoney1;
                    Economics?.CallHook("SetMoney", player.userID, money);
                    if (buyMoney1 >= 1)
                    {
                        SendReply(player, string.Format(lang.GetMessage(NpcName, this) + lang.GetMessage("charged", this, player.UserIDString), (int)(buyMoney1)));
                    }
                }
            }

            if (useRewards)
            {
                var money = (int)ServerRewards.Call("CheckPoints", player.userID);
                if (money < buyMoney1)
                {
                    SendReply(player, string.Format(lang.GetMessage(NpcName, this) + lang.GetMessage("nomoney", this, player.UserIDString), (int)(buyMoney1)));
                    return;
                }

                if (money >= buyMoney1)
                {
                    ServerRewards.Call("TakePoints", player.userID, (int)buyMoney1);
                    if (buyMoney1 >= 1)
                    {
                        SendReply(player, string.Format(lang.GetMessage(NpcName, this) + lang.GetMessage("charged", this, player.UserIDString), (int)(buyMoney1)));
                    }
                }
            }			

            if (useItem)
            {
                var amount = GetAmount(player, ItemShortName);
                if (amount < ItemAmounts)
                {
                var definition = ItemManager.FindItemDefinition(ItemShortName);	
				var item = definition.displayName.english;
                SendReply(player, "You have {0} {1} and you need {2}", amount, item, ItemAmounts);
				return;
                }
                if (amount >= ItemAmounts)
                {
                    TakeResources(player, ItemShortName, ItemAmounts);
                    if (ItemAmounts >= 1)
                    {
				var definition = ItemManager.FindItemDefinition(ItemShortName);	
				var item = definition.displayName.english;
                        SendReply(player, string.Format(lang.GetMessage(NpcName, this) + lang.GetMessage("chargedItem", this, player.UserIDString), (int)(ItemAmounts), item));
                    }
                }
            }						
			
            if (UseCommand == false)
            {
                pcdData.pCooldown[playerId].npcCooldowns[npcId] = (long)timeStamp + cooldownTime; // Store the new cooldown in the players data under the specified NPC
                SaveData();
                object success = null;
                if (spawn != "random" || Spawns != null)
                success = Spawns.Call("GetRandomSpawn", spawn);
                if (spawn == "random")
                success = GetRandomVector(new Vector3(0, 0, 0), max);
                if (success is Vector3) // Check if the returned type is Vector3
                {
                    TeleportPlayerPosition1(player, (Vector3)success);
                }
                else PrintError((string)bad); // Otherwise print the error message to console so server owners know there is a problem
            }
            if (UseCommand == true && CommandOnPlayer == false)
            {
                pcdData.pCooldown[playerId].npcCooldowns[npcId] = (long)timeStamp + cooldownTime;
                SaveData();

                if (!CommandOnPlayer) // Check if this is command on player
                {
                    rust.RunServerCommand($"{Command} {Arrangements}");
                    SendReply(player, string.Format(lang.GetMessage(NpcName, this) + lang.GetMessage("npcCommand", this)));
                }
                else PrintError((string)bad1); // Otherwise print the error message to console so server owners know there is a problem
            }
            if (UseCommand == true && CommandOnPlayer == true)
            {
                pcdData.pCooldown[playerId].npcCooldowns[npcId] = (long)timeStamp + cooldownTime;
                SaveData();

                if (CommandOnPlayer) // Check if this is command on player
                {
                    rust.RunServerCommand($"{Command} {player.userID} {Arrangements}");
                    SendReply(player, string.Format(lang.GetMessage(NpcName, this) + lang.GetMessage("npcCommand", this)));
                }
                else PrintError((string)bad2); // Otherwise print the error message to console so server owners know there is a problem
            }
            if (OpenDoor == true && DoorId > 0)
            {
                var newLocation = new Vector3(npcData.NpcTP[npcId].DoorLocX, npcData.NpcTP[npcId].DoorLocY, npcData.NpcTP[npcId].DoorLocZ);
                List<BaseEntity> doornear = new List<BaseEntity>();
                Vis.Entities(newLocation, 1.5f, doornear);
                var i = 0;
                foreach (var door in doornear)
                {
                    if (!door.IsOpen() && (door.GetComponent<Door>()))
                    {
                        i++;
                        door.SetFlag(BaseEntity.Flags.Open, true);
                        door.SendNetworkUpdateImmediate();
                        timer.Once(AutoCloseTime, () =>
                    {
                        if (!door.IsOpen()) return;
                        i++;
                        door.SetFlag(BaseEntity.Flags.Open, false);
                        door.SendNetworkUpdateImmediate();

                    });
                    }
                    else { }
                }

                if (DoorOpenMessage)
                    SendReply(player, string.Format(lang.GetMessage(NpcName, this) + lang.GetMessage("DoorMessage", this)));
                return;
            }
	}
        readonly Dictionary<ulong, Timer> timers = new Dictionary<ulong, Timer>();


        void OnEnterNPC(BasePlayer npc, BasePlayer player)
        {

	    if (!player.userID.IsSteamId()) return;
            string npcId = npc.UserIDString;
            if (!npcData.NpcTP.ContainsKey(npc.UserIDString)) return;
            string Perms = npcData.NpcTP[npcId].permission;
            var CanUse = npcData.NpcTP[npcId].CanUse;
            if (!CanUse) return;
            var NoPermDie = npcData.NpcTP[npcId].NoPermKill;
            var KillSleepA = npcData.NpcTP[npcId].KillSleep;
            if ((useJail) && (bool) (Jail?.CallHook("IsPrisoner", player) ?? false))
            {
                return;
            }

            if (player.IsSleeping() && KillSleepA == true)
                applySleepDamage(player);

            if (NoPermDie == false || permission.UserHasPermission(player.userID.ToString(), Perms) || KillSleepA == true || !player.userID.IsSteamId()) return;
            if (UseGunToKill)
                StartNpcAttack(npc, player);
            if (!SetOnFire) return;
            StartFire(player);
        }
        void OnLeaveNPC(BasePlayer npc, BasePlayer player)
        {
			if (!player.userID.IsSteamId()) return;
            string npcId = npc.UserIDString;
            if (!npcData.NpcTP.ContainsKey(npc.UserIDString)) return;
            string Perms = npcData.NpcTP[npcId].permission;
            var CanUse = npcData.NpcTP[npcId].CanUse;
            if (!CanUse) return;
            var NoPermDie = npcData.NpcTP[npcId].NoPermKill;

            if (timers.ContainsKey(player.userID))
                timers[player.userID].Destroy();
            if (NoPermDie == false || permission.UserHasPermission(player.userID.ToString(), Perms) || FollowDead == true) return;
            var humanPlayer = npc.GetComponent<HumanNPC.HumanPlayer>();
            humanPlayer.info.hostile = false;
            var locomotion = npc.GetComponent<HumanNPC.HumanLocomotion>();
            Interface.Oxide.CallHook("OnNPCStopTarget", player, locomotion.attackEntity);
            locomotion.attackEntity = null;
            locomotion.GetBackToLastPos();
        }

        void StartNpcAttack(BasePlayer npc, BasePlayer player)
        {
            var humanPlayer = npc.GetComponent<HumanNPC.HumanPlayer>();
            if (!(bool)(Vanish?.CallHook("IsInvisible", player) ?? false))
                humanPlayer.info.hostile = true;
            HumanNPC?.Call("OnEnterNPC", npc, player);
            return;
        }

        void StartFire(BasePlayer player)
        {

            var playerPos = player.transform.position;
            var playerRot = player.transform.rotation;
            BaseEntity FireBurn = GameManager.server.CreateEntity("assets/bundled/prefabs/oilfireballsmall.prefab", playerPos, playerRot);

            timer.Once(OnFireTime, () => FireBurn.Kill());
            FireBurn.Spawn();
            FireBurn.SetParent(player);
            FireBurn.transform.localPosition = new Vector3(0f, 0f, 0f);
            applyBlastDamage(player);
            ((Rigidbody)FireBurn.gameObject.GetComponent(typeof(Rigidbody))).isKinematic = true;
            return;
        }

        void applyBlastDamage(BasePlayer player)
        {
            if (player != null || !timers.ContainsKey(player.userID))
                timers[player.userID] = timer.Every(2, () =>
                {
                    if (player.IsDead() && timers.ContainsKey(player.userID))
                        timers[player.userID].Destroy();
                    else
                        player.Hurt(OnFireDamage);
                });
            return;
        }
        void applySleepDamage(BasePlayer player)
        {
            if (player != null)
            {
                if (player.IsDead())
                    return;
                else
                    player.Hurt(SleepKillDamage);
                return;
            }
        }

        void OnUseNPC(BasePlayer npc, BasePlayer player, Vector3 destination)
        {
            if (!npcData.NpcTP.ContainsKey(npc.UserIDString)) return; // Check if this NPC is registered

            ulong playerId = player.userID;
            string npcId = npc.UserIDString;
            double timeStamp = GrabCurrentTime();
            var CanUse = npcData.NpcTP[npcId].CanUse;
            var useUI = npcData.NpcTP[npcId].useUI;
            var cooldownTime = npcData.NpcTP[npcId].Cooldown;
            var Perms = npcData.NpcTP[npcId].permission;
            var amount = npcData.NpcTP[npcId].Cost;
            var useMessage = npcData.NpcTP[npcId].useMessage;
            var MessageNpc = npcData.NpcTP[npcId].MessageNpc;
            var DoorId = npcData.NpcTP[npcId].DoorId;
            var doorcheck = Convert.ToString(DoorId);
            var OpenDoor = npcData.NpcTP[npcId].OpenDoor;
            string NpcName = npcData.NpcTP[npcId].NpcName;

            if (!pcdData.pCooldown.ContainsKey(playerId))
            {
                pcdData.pCooldown.Add(playerId, new PCDInfo());
                //SaveData();
            }

            if (!CanUse)
            {
                SendReply(player, string.Format(lang.GetMessage(NpcName, this) + lang.GetMessage("notenabled", this)));
                return;
            }
            else
            {
                if (!permission.UserHasPermission(player.userID.ToString(), Perms))
                {
                    SendReply(player, string.Format(lang.GetMessage(NpcName, this) + lang.GetMessage("noperm", this)));
                    return;
                }
                if (pcdData.pCooldown[playerId].npcCooldowns.ContainsKey(npcId)) // Check if the player already has a cooldown for this NPC
                {
                    var cdTime = pcdData.pCooldown[playerId].npcCooldowns[npcId]; // Get the cooldown time of the NPC
                    if (cdTime > timeStamp)
                    {
                        SendReply(player, string.Format(lang.GetMessage(NpcName, this) + lang.GetMessage("cdTime", this, player.UserIDString), (int)(cdTime - timeStamp) / 60));
                        return;
                    }
                }
                if (OpenDoor == true && DoorId > 0)
                {
                    var newLocation = new Vector3(npcData.NpcTP[npcId].DoorLocX, npcData.NpcTP[npcId].DoorLocY, npcData.NpcTP[npcId].DoorLocZ);
                    List<BaseEntity> doornear = new List<BaseEntity>();
                    Vis.Entities(newLocation, 1.0f, doornear);
                    var i = 0;
                    foreach (var door in doornear)
                    {
                        if (door.ToString().Contains(doorcheck) && door.ToString().Contains("hinged"))
                            if (door.IsOpen())
                            {
                                SendReply(player, string.Format(lang.GetMessage(NpcName, this) + lang.GetMessage("DoorIsOpen", this)));
                                return;
                            }
                    }
                }
                if (!useUI && !useMessage)
                {
                    player.SendConsoleCommand($"hardestcommandtoeverguessnpctp {playerId} {npcId}");
                    return;
                }
                else
                if (useUI && amount >= 1 || useMessage)
                {

                    var elements = new CuiElementContainer();
                    msg = (useMessage && amount >= 1) ? MessageNpc + "\n \n" + lang.GetMessage("commandyesno", this, player.UserIDString) + amount + lang.GetMessage("commandyesno1", this) : "";
                    if (msg == "")
                        msg = (!useMessage && amount >= 1) ? "\n \n" + lang.GetMessage("commandyesno", this, player.UserIDString) + amount + lang.GetMessage("commandyesno1", this) : "";
                    if (msg == "")
                        msg = (useMessage && amount == 0) ? MessageNpc : "";
                    if (msg == "")
                        //Sets the msg to unknown as we could not find a correct variable to match it with.
                        msg = "Unknown";
                    {
                        var mainName = elements.Add(new CuiPanel
                        {
                            Image =
                {
                    Color = "0.1 0.1 0.1 1"
                },
                            RectTransform =
                {
                    AnchorMin = "0 0",
                    AnchorMax = "1 1"
                },
                            CursorEnabled = true
                        }, "Overlay", "Npctp");
                        if (backroundimage == true)
                        {
                            elements.Add(new CuiElement
                            {
                                Parent = "Npctp",
                                Components =
                    {
                        new CuiRawImageComponent
                        {
                            Url = backroundimageurl,
                            Sprite = "assets/content/textures/generic/fulltransparent.tga"
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "1 1"
                        }
                    }
                            });
                        }
                        var Agree = new CuiButton
                        {
                            Button =
                {
                    Command = $"hardestcommandtoeverguessnpctp {playerId} {npcId}",
                    Close = mainName,
                    Color = "0 255 0 1"
                },
                            RectTransform =
                {
                    AnchorMin = "0.2 0.16",
                    AnchorMax = "0.45 0.2"
                },
                            Text =
                {
                    Text = "Go",
                    FontSize = 22,
                    Align = TextAnchor.MiddleCenter
                }
                        };
                        var Disagree = new CuiButton
                        {


                            Button =
                {

                    Close = mainName,
                    Color = "255 0 0 1"

                },
                            RectTransform =
                {
                    AnchorMin = "0.5 0.16",
                    AnchorMax = "0.75 0.2"
                },
                            Text =
                {
                    Text = "Cancel",
                    FontSize = 22,
                    Align = TextAnchor.MiddleCenter
                }
                        };
                        elements.Add(new CuiLabel
                        {
                            Text =
                {
                    Text = msg,
                    FontSize = 22,
                    Align = TextAnchor.MiddleCenter
                },
                            RectTransform =
                {
                    AnchorMin = "0 0.20",
                    AnchorMax = "1 0.9"
                }
                        }, mainName);
                        elements.Add(Agree, mainName);
                        elements.Add(Disagree, mainName);
                        CuiHelper.AddUi(player, elements);
                    }
                }
            }

            #endregion



        }

    }

}





